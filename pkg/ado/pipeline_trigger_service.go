package ado

import (
	"context"
	"encoding/json"
	"fmt"
	"net/url"
	"strings"

	"github.com/github/gh-gei/pkg/logger"
)

const unknownRepoIdentifier = "unknown"

// adoRepo holds repository info used internally by PipelineTriggerService.
type adoRepo struct {
	ID         string `json:"id"`
	Name       string `json:"name"`
	IsDisabled bool   `json:"isDisabled"`
}

// rawAPIClient is the interface for raw HTTP calls used by PipelineTriggerService.
type rawAPIClient interface {
	GetRaw(ctx context.Context, url string) (string, error)
	PutRaw(ctx context.Context, url string, payload interface{}) (string, error)
}

// PipelineTriggerService manages pipeline trigger configuration during repository rewiring.
type PipelineTriggerService struct {
	api        rawAPIClient
	log        *logger.Logger
	adoBaseURL string

	// Caches to avoid redundant API calls
	repoCache   map[string]adoRepo
	policyCache map[string]*BranchPolicyResponse
}

// NewPipelineTriggerService creates a new PipelineTriggerService.
func NewPipelineTriggerService(api rawAPIClient, log *logger.Logger, adoBaseURL string) *PipelineTriggerService {
	return &PipelineTriggerService{
		api:         api,
		log:         log,
		adoBaseURL:  strings.TrimRight(adoBaseURL, "/"),
		repoCache:   make(map[string]adoRepo),
		policyCache: make(map[string]*BranchPolicyResponse),
	}
}

// RewirePipelineToGitHub changes a pipeline's repository configuration from ADO to GitHub,
// applying trigger configuration based on branch policy requirements and existing settings.
// Returns true if the pipeline was successfully rewired, false if it was skipped.
func (s *PipelineTriggerService) RewirePipelineToGitHub(
	ctx context.Context,
	adoOrg, teamProject string,
	pipelineId int,
	defaultBranch, clean, checkoutSubmodules string,
	githubOrg, githubRepo, connectedServiceId string,
	originalTriggers json.RawMessage,
	targetApiUrl string,
) (bool, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions/%d?api-version=6.0",
		s.adoBaseURL, url.PathEscape(adoOrg), url.PathEscape(teamProject), pipelineId)

	response, err := s.api.GetRaw(ctx, apiURL)
	if err != nil {
		if strings.Contains(err.Error(), "404") {
			s.log.Warning("Pipeline %d not found in %s/%s. Skipping pipeline rewiring.", pipelineId, adoOrg, teamProject)
			return false, nil
		}
		s.log.Warning("HTTP error retrieving pipeline %d in %s/%s: %v. Skipping pipeline rewiring.", pipelineId, adoOrg, teamProject, err)
		return false, nil
	}

	var data map[string]interface{}
	if err := json.Unmarshal([]byte(response), &data); err != nil {
		return false, fmt.Errorf("parse pipeline definition: %w", err)
	}

	currentRepoName := ""
	currentRepoId := ""
	if repo, ok := data["repository"].(map[string]interface{}); ok {
		if name, ok := repo["name"].(string); ok {
			currentRepoName = name
		}
		if id, ok := repo["id"].(string); ok {
			currentRepoId = id
		}
	}

	// Detect pipeline process type: 1 = Classic/Designer, 2 = YAML
	processType := 2 // default to YAML
	if process, ok := data["process"].(map[string]interface{}); ok {
		if pt, ok := process["type"].(float64); ok {
			processType = int(pt)
		}
	}

	newRepo := s.createGitHubRepositoryConfiguration(githubOrg, githubRepo, defaultBranch, clean, checkoutSubmodules, connectedServiceId, targetApiUrl)
	isPipelineRequired, err := s.IsPipelineRequiredByBranchPolicy(ctx, adoOrg, teamProject, currentRepoName, currentRepoId, pipelineId)
	if err != nil {
		return false, err
	}

	s.logBranchPolicyCheckResults(pipelineId, isPipelineRequired)

	payload := s.buildPipelinePayload(data, newRepo, originalTriggers, isPipelineRequired, processType)

	if _, err := s.api.PutRaw(ctx, apiURL, payload); err != nil {
		return false, fmt.Errorf("update pipeline definition: %w", err)
	}
	return true, nil
}

// IsPipelineRequiredByBranchPolicy analyzes branch policies to determine if a pipeline
// is required for branch protection.
func (s *PipelineTriggerService) IsPipelineRequiredByBranchPolicy(
	ctx context.Context,
	adoOrg, teamProject, repoName, repoId string,
	pipelineId int,
) (bool, error) {
	if repoName == "" && repoId == "" {
		s.log.Warning("Branch policy check skipped for pipeline %d - repository name and ID not available. Pipeline trigger configuration may not preserve branch policy requirements.", pipelineId)
		return false, nil
	}

	repoInfo, err := s.getRepositoryIdAndStatus(ctx, adoOrg, teamProject, repoName, repoId, pipelineId)
	if err != nil {
		s.logBranchPolicyCheckError(err, adoOrg, teamProject, repoName, repoId, pipelineId)
		return false, nil
	}

	if repoInfo.ID == "" {
		return false, nil
	}

	if repoInfo.IsDisabled {
		repoIdentifier := repoName
		if repoIdentifier == "" {
			repoIdentifier = repoId
		}
		s.log.Info("Repository %s/%s/%s is disabled. Branch policy check skipped for pipeline %d - will use default trigger configuration.", adoOrg, teamProject, repoIdentifier, pipelineId)
		return false, nil
	}

	return s.checkBranchPoliciesForPipeline(ctx, adoOrg, teamProject, repoInfo.ID, repoName, repoId, pipelineId)
}

func (s *PipelineTriggerService) getRepositoryIdAndStatus(
	ctx context.Context,
	adoOrg, teamProject, repoName, repoId string,
	pipelineId int,
) (adoRepo, error) {
	if repoId != "" {
		s.log.Verbose("Using repository ID from pipeline definition for branch policy check: %s", repoId)
		repoInfo, err := s.getRepositoryInfoWithCache(ctx, adoOrg, teamProject, repoId, repoName)
		if err != nil {
			return adoRepo{}, err
		}
		return adoRepo{ID: repoId, Name: repoName, IsDisabled: repoInfo.IsDisabled}, nil
	}

	repoInfo, err := s.getRepositoryInfoWithCache(ctx, adoOrg, teamProject, "", repoName)
	if err != nil {
		return adoRepo{}, err
	}
	if repoInfo.ID == "" {
		s.log.Warning("Repository ID not found for %s/%s/%s. Branch policy check cannot be performed for pipeline %d.", adoOrg, teamProject, repoName, pipelineId)
		return adoRepo{Name: repoName}, nil
	}

	return repoInfo, nil
}

func (s *PipelineTriggerService) checkBranchPoliciesForPipeline(
	ctx context.Context,
	adoOrg, teamProject, repositoryId, repoName, repoId string,
	pipelineId int,
) (bool, error) {
	policyData, err := s.getBranchPoliciesWithCache(ctx, adoOrg, teamProject, repositoryId)
	if err != nil {
		s.logBranchPolicyCheckError(err, adoOrg, teamProject, repoName, repoId, pipelineId)
		return false, nil
	}

	if policyData == nil || len(policyData.Value) == 0 {
		repoIdentifier := repoName
		if repoIdentifier == "" {
			repoIdentifier = repoId
		}
		if repoIdentifier == "" {
			repoIdentifier = unknownRepoIdentifier
		}
		s.log.Verbose("No branch policies found for repository %s/%s/%s. ADO Pipeline ID = %d is not required by branch policy.", adoOrg, teamProject, repoIdentifier, pipelineId)
		return false, nil
	}

	pipelineIdStr := fmt.Sprintf("%d", pipelineId)
	isPipelineRequired := false
	for _, policy := range policyData.Value {
		if policy.Type.DisplayName == "Build" && policy.IsEnabled && policy.Settings.BuildDefinitionId == pipelineIdStr {
			isPipelineRequired = true
			break
		}
	}

	s.logBranchPolicyCheckResult(isPipelineRequired, adoOrg, teamProject, repoName, repoId, pipelineId)
	return isPipelineRequired, nil
}

func (s *PipelineTriggerService) logBranchPolicyCheckResult(isPipelineRequired bool, adoOrg, teamProject, repoName, repoId string, pipelineId int) {
	repoIdentifier := repoName
	if repoIdentifier == "" {
		repoIdentifier = repoId
	}
	if repoIdentifier == "" {
		repoIdentifier = unknownRepoIdentifier
	}

	if isPipelineRequired {
		s.log.Verbose("ADO Pipeline ID = %d is required by branch policy in %s/%s/%s. Build status reporting will be enabled to support branch protection.", pipelineId, adoOrg, teamProject, repoIdentifier)
	} else {
		s.log.Verbose("ADO Pipeline ID = %d is not required by any branch policies in %s/%s/%s.", pipelineId, adoOrg, teamProject, repoIdentifier)
	}
}

func (s *PipelineTriggerService) logBranchPolicyCheckError(err error, adoOrg, teamProject, repoName, repoId string, pipelineId int) {
	repoIdentifier := repoName
	if repoIdentifier == "" {
		repoIdentifier = repoId
	}
	if repoIdentifier == "" {
		repoIdentifier = unknownRepoIdentifier
	}
	s.log.Warning("Error during branch policy check for pipeline %d in %s/%s/%s: %v. Pipeline trigger configuration may not preserve branch policy requirements.", pipelineId, adoOrg, teamProject, repoIdentifier, err)
}

func (s *PipelineTriggerService) logBranchPolicyCheckResults(pipelineId int, isPipelineRequired bool) {
	if isPipelineRequired {
		s.log.Info("ADO Pipeline ID = %d IS required by branch policy - enabling build status reporting to support branch protection", pipelineId)
	} else {
		s.log.Info("ADO Pipeline ID = %d is NOT required by branch policy - preserving original trigger configuration", pipelineId)
	}
}

// ---------------------------------------------------------------------------
// Trigger configuration logic
// ---------------------------------------------------------------------------

func (s *PipelineTriggerService) buildPipelinePayload(data map[string]interface{}, newRepo interface{}, originalTriggers json.RawMessage, isPipelineRequired bool, processType int) map[string]interface{} {
	isClassicPipeline := processType == 1
	payload := make(map[string]interface{})

	for key, val := range data {
		switch key {
		case "repository":
			payload[key] = newRepo
		case "triggers":
			// Classic pipelines keep their original triggers; YAML pipelines get reconfigured
			if isClassicPipeline {
				if originalTriggers != nil && string(originalTriggers) != nullStr {
					var parsed interface{}
					if err := json.Unmarshal(originalTriggers, &parsed); err == nil {
						payload[key] = parsed
					} else {
						payload[key] = val
					}
				} else {
					payload[key] = val
				}
			} else {
				payload[key] = s.determineTriggerConfiguration(originalTriggers, isPipelineRequired)
			}
		default:
			payload[key] = val
		}
	}

	if !isClassicPipeline {
		// Add triggers if no triggers property existed (YAML pipelines only)
		if _, ok := payload["triggers"]; !ok {
			payload["triggers"] = s.determineTriggerConfiguration(originalTriggers, isPipelineRequired)
		}
	}

	// settingsSourceType: 1 = UI/Designer override (Classic), 2 = YAML definitions
	if isClassicPipeline {
		payload["settingsSourceType"] = 1
	} else {
		payload["settingsSourceType"] = 2
	}

	return payload
}

func (s *PipelineTriggerService) createGitHubRepositoryConfiguration(
	githubOrg, githubRepo, defaultBranch, clean, checkoutSubmodules, connectedServiceId, targetApiUrl string,
) map[string]interface{} {
	apiUrl, _, cloneUrl, branchesUrl, refsUrl, manageUrl := s.buildGitHubUrls(githubOrg, githubRepo, targetApiUrl)

	return map[string]interface{}{
		"properties": map[string]interface{}{
			"apiUrl":             apiUrl,
			"branchesUrl":        branchesUrl,
			"cloneUrl":           cloneUrl,
			"connectedServiceId": connectedServiceId,
			"defaultBranch":      defaultBranch,
			"fullName":           fmt.Sprintf("%s/%s", githubOrg, githubRepo),
			"manageUrl":          manageUrl,
			"orgName":            githubOrg,
			"refsUrl":            refsUrl,
			"safeRepository":     fmt.Sprintf("%s/%s", url.PathEscape(githubOrg), url.PathEscape(githubRepo)),
			"shortName":          githubRepo,
			"reportBuildStatus":  "true",
		},
		"id":                 fmt.Sprintf("%s/%s", githubOrg, githubRepo),
		"type":               "GitHub",
		"name":               fmt.Sprintf("%s/%s", githubOrg, githubRepo),
		"url":                cloneUrl,
		"defaultBranch":      defaultBranch,
		"clean":              clean,
		"checkoutSubmodules": checkoutSubmodules,
	}
}

func (s *PipelineTriggerService) determineTriggerConfiguration(originalTriggers json.RawMessage, isPipelineRequired bool) interface{} {
	if isPipelineRequired {
		return s.createBranchPolicyRequiredTriggers(originalTriggers)
	}
	return s.createStandardTriggers(originalTriggers)
}

func (s *PipelineTriggerService) createBranchPolicyRequiredTriggers(originalTriggers json.RawMessage) interface{} {
	originalCiReport := s.getOriginalReportBuildStatus(originalTriggers, "continuousIntegration")
	originalPrReport := s.getOriginalReportBuildStatus(originalTriggers, "pullRequest")

	enableCiBuildStatus := originalCiReport || originalTriggers == nil || !s.hasTriggerType(originalTriggers, "continuousIntegration")
	enablePrBuildStatus := originalPrReport || originalTriggers == nil || !s.hasTriggerType(originalTriggers, "pullRequest")

	return s.createYamlControlledTriggers(true, enableCiBuildStatus, enablePrBuildStatus)
}

func (s *PipelineTriggerService) createStandardTriggers(originalTriggers json.RawMessage) interface{} {
	if originalTriggers != nil && string(originalTriggers) != "null" {
		hadPullRequestTrigger := s.hasPullRequestTrigger(originalTriggers)
		originalCiReport := s.getOriginalReportBuildStatus(originalTriggers, "continuousIntegration")
		originalPrReport := s.getOriginalReportBuildStatus(originalTriggers, "pullRequest")
		return s.createYamlControlledTriggers(hadPullRequestTrigger, originalCiReport, originalPrReport)
	}

	// Default: enable PR validation with build status reporting for backwards compatibility
	return s.createYamlControlledTriggers(true, true, true)
}

func (s *PipelineTriggerService) createYamlControlledTriggers(enablePullRequestValidation, enableCiBuildStatusReporting, enablePrBuildStatusReporting bool) []map[string]interface{} {
	ciTrigger := map[string]interface{}{
		"triggerType":        "continuousIntegration",
		"settingsSourceType": 2,
		"branchFilters":      []interface{}{},
		"pathFilters":        []interface{}{},
		"batchChanges":       false,
	}

	if enableCiBuildStatusReporting {
		ciTrigger["reportBuildStatus"] = "true"
	}

	triggers := []map[string]interface{}{ciTrigger}

	if enablePullRequestValidation {
		prTrigger := map[string]interface{}{
			"triggerType":                          "pullRequest",
			"settingsSourceType":                   2,
			"isCommentRequiredForPullRequest":      false,
			"requireCommentsForNonTeamMembersOnly": false,
			"forks": map[string]interface{}{
				"enabled":      false,
				"allowSecrets": false,
			},
			"branchFilters": []interface{}{},
			"pathFilters":   []interface{}{},
		}

		if enablePrBuildStatusReporting {
			prTrigger["reportBuildStatus"] = "true"
		}

		triggers = append(triggers, prTrigger)
	}

	return triggers
}

// ---------------------------------------------------------------------------
// Trigger analysis helpers
// ---------------------------------------------------------------------------

func (s *PipelineTriggerService) hasPullRequestTrigger(originalTriggers json.RawMessage) bool {
	if originalTriggers == nil {
		return false
	}
	var triggers []map[string]interface{}
	if err := json.Unmarshal(originalTriggers, &triggers); err != nil {
		return false
	}
	for _, t := range triggers {
		if tt, ok := t["triggerType"].(string); ok && tt == "pullRequest" {
			return true
		}
	}
	return false
}

func (s *PipelineTriggerService) getOriginalReportBuildStatus(originalTriggers json.RawMessage, triggerType string) bool {
	if originalTriggers == nil || string(originalTriggers) == "null" {
		return true // Default to true when no original triggers exist
	}

	var triggers []map[string]interface{}
	if err := json.Unmarshal(originalTriggers, &triggers); err != nil {
		return true // Default to true on parse error
	}

	for _, t := range triggers {
		tt, ok := t["triggerType"].(string)
		if !ok || tt != triggerType {
			continue
		}

		rbs, exists := t["reportBuildStatus"]
		if !exists {
			return true // Default to true when property doesn't exist
		}

		switch v := rbs.(type) {
		case bool:
			return v
		case string:
			return strings.EqualFold(v, "true")
		default:
			return true // Default to true for unexpected types
		}
	}

	return true // Default to true when trigger type not found
}

func (s *PipelineTriggerService) hasTriggerType(originalTriggers json.RawMessage, triggerType string) bool {
	if originalTriggers == nil {
		return false
	}
	var triggers []map[string]interface{}
	if err := json.Unmarshal(originalTriggers, &triggers); err != nil {
		return false
	}
	for _, t := range triggers {
		if tt, ok := t["triggerType"].(string); ok && tt == triggerType {
			return true
		}
	}
	return false
}

// ---------------------------------------------------------------------------
// URL helpers
// ---------------------------------------------------------------------------

func (s *PipelineTriggerService) buildGitHubUrls(githubOrg, githubRepo, targetApiUrl string) (apiUrl, webUrl, cloneUrl, branchesUrl, refsUrl, manageUrl string) {
	escapedOrg := url.PathEscape(githubOrg)
	escapedRepo := url.PathEscape(githubRepo)

	if targetApiUrl != "" {
		targetApiUrl = strings.TrimRight(targetApiUrl, "/")
		parsed, err := url.Parse(targetApiUrl)
		if err != nil {
			// Fall through to default behavior if URL is invalid
			return s.buildDefaultGitHubUrls(escapedOrg, escapedRepo)
		}

		webHost := strings.TrimPrefix(parsed.Host, "api.")
		webBase := fmt.Sprintf("%s://%s", parsed.Scheme, webHost)

		apiUrl = fmt.Sprintf("%s/repos/%s/%s", targetApiUrl, escapedOrg, escapedRepo)
		webUrl = fmt.Sprintf("%s/%s/%s", webBase, escapedOrg, escapedRepo)
		cloneUrl = fmt.Sprintf("%s/%s/%s.git", webBase, escapedOrg, escapedRepo)
		branchesUrl = fmt.Sprintf("%s/repos/%s/%s/branches", targetApiUrl, escapedOrg, escapedRepo)
		refsUrl = fmt.Sprintf("%s/repos/%s/%s/git/refs", targetApiUrl, escapedOrg, escapedRepo)
		manageUrl = webUrl
		return
	}

	return s.buildDefaultGitHubUrls(escapedOrg, escapedRepo)
}

func (s *PipelineTriggerService) buildDefaultGitHubUrls(escapedOrg, escapedRepo string) (apiUrl, webUrl, cloneUrl, branchesUrl, refsUrl, manageUrl string) {
	apiUrl = fmt.Sprintf("https://api.github.com/repos/%s/%s", escapedOrg, escapedRepo)
	webUrl = fmt.Sprintf("https://github.com/%s/%s", escapedOrg, escapedRepo)
	cloneUrl = fmt.Sprintf("https://github.com/%s/%s.git", escapedOrg, escapedRepo)
	branchesUrl = fmt.Sprintf("https://api.github.com/repos/%s/%s/branches", escapedOrg, escapedRepo)
	refsUrl = fmt.Sprintf("https://api.github.com/repos/%s/%s/git/refs", escapedOrg, escapedRepo)
	manageUrl = webUrl
	return
}

// ---------------------------------------------------------------------------
// Caching helpers
// ---------------------------------------------------------------------------

func (s *PipelineTriggerService) getRepositoryInfoWithCache(ctx context.Context, adoOrg, teamProject, repoId, repoName string) (adoRepo, error) {
	identifier := repoId
	if identifier == "" {
		identifier = repoName
	}
	cacheKey := strings.ToUpper(fmt.Sprintf("%s/%s/%s", adoOrg, teamProject, identifier))

	if cached, ok := s.repoCache[cacheKey]; ok {
		s.log.Verbose("Using cached repository information for %s/%s/%s", adoOrg, teamProject, identifier)
		return cached, nil
	}

	s.log.Verbose("Fetching repository information for %s/%s/%s", adoOrg, teamProject, identifier)

	repoURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s?api-version=6.0",
		s.adoBaseURL, url.PathEscape(adoOrg), url.PathEscape(teamProject), url.PathEscape(identifier))

	response, err := s.api.GetRaw(ctx, repoURL)
	if err != nil {
		if strings.Contains(err.Error(), "404") {
			s.log.Verbose("Repository %s/%s/%s returned 404 - likely disabled or not found.", adoOrg, teamProject, identifier)
			info := adoRepo{Name: identifier, IsDisabled: true}
			s.repoCache[cacheKey] = info
			return info, nil
		}
		s.log.Verbose("Failed to fetch repository information for %s/%s/%s: %v", adoOrg, teamProject, identifier, err)
		return adoRepo{}, err
	}

	var repoData struct {
		ID         string `json:"id"`
		IsDisabled bool   `json:"isDisabled"`
	}
	if err := json.Unmarshal([]byte(response), &repoData); err != nil {
		s.log.Verbose("JSON parsing error for repository %s/%s/%s: %v", adoOrg, teamProject, identifier, err)
		return adoRepo{}, err
	}

	if repoData.ID != "" {
		info := adoRepo{ID: repoData.ID, Name: identifier, IsDisabled: repoData.IsDisabled}
		s.repoCache[cacheKey] = info
		s.log.Verbose("Cached repository information (ID: %s, Disabled: %t) for %s/%s/%s", repoData.ID, repoData.IsDisabled, adoOrg, teamProject, identifier)
		return info, nil
	}

	return adoRepo{Name: identifier}, nil
}

func (s *PipelineTriggerService) getBranchPoliciesWithCache(ctx context.Context, adoOrg, teamProject, repositoryId string) (*BranchPolicyResponse, error) {
	cacheKey := strings.ToUpper(fmt.Sprintf("%s/%s/%s", adoOrg, teamProject, repositoryId))

	if cached, ok := s.policyCache[cacheKey]; ok {
		s.log.Verbose("Using cached branch policies for repository ID %s", repositoryId)
		return cached, nil
	}

	s.log.Verbose("Fetching branch policies for repository ID %s", repositoryId)

	policyURL := fmt.Sprintf("%s/%s/%s/_apis/policy/configurations?repositoryId=%s&api-version=6.0",
		s.adoBaseURL, url.PathEscape(adoOrg), url.PathEscape(teamProject), repositoryId)

	response, err := s.api.GetRaw(ctx, policyURL)
	if err != nil {
		s.log.Verbose("Failed to fetch branch policies for repository ID %s: %v", repositoryId, err)
		return nil, err
	}

	var policyData BranchPolicyResponse
	if err := json.Unmarshal([]byte(response), &policyData); err != nil {
		s.log.Verbose("JSON parsing error for branch policies repository ID %s: %v", repositoryId, err)
		return nil, err
	}

	s.policyCache[cacheKey] = &policyData
	s.log.Verbose("Cached %d branch policies for repository ID %s", len(policyData.Value), repositoryId)

	return &policyData, nil
}
