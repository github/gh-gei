package ado

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// pipelineTestAdoAPI defines the ADO API methods needed by PipelineTestService.
type pipelineTestAdoAPI interface {
	GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error)
	IsPipelineEnabled(ctx context.Context, org, teamProject string, pipelineId int) (bool, error)
	GetPipelineRepository(ctx context.Context, org, teamProject string, pipelineId int) (PipelineRepository, error)
	GetPipeline(ctx context.Context, org, teamProject string, pipelineId int) (PipelineInfo, error)
	QueueBuild(ctx context.Context, org, teamProject string, pipelineId int, sourceBranch string) (int, error)
	GetBuildStatus(ctx context.Context, org, teamProject string, buildId int) (BuildStatus, error)
	RestorePipelineToAdoRepo(ctx context.Context, org, teamProject string, pipelineId int, adoRepoName, defaultBranch, clean, checkoutSubmodules string, originalTriggers json.RawMessage) error
}

// pipelineRewirer defines the pipeline rewiring capability.
type pipelineRewirer interface {
	RewirePipelineToGitHub(ctx context.Context, adoOrg, teamProject string, pipelineId int, defaultBranch, clean, checkoutSubmodules string, githubOrg, githubRepo, connectedServiceId string, originalTriggers json.RawMessage, targetApiUrl string) (bool, error)
}

// ---------------------------------------------------------------------------
// PipelineTestArgs
// ---------------------------------------------------------------------------

// PipelineTestArgs holds the arguments for testing a single pipeline.
type PipelineTestArgs struct {
	AdoOrg                string
	AdoTeamProject        string
	PipelineName          string
	PipelineId            *int
	GithubOrg             string
	GithubRepo            string
	ServiceConnectionId   string
	TargetApiUrl          string
	MonitorTimeoutMinutes int
}

// ---------------------------------------------------------------------------
// PipelineTestService
// ---------------------------------------------------------------------------

// PipelineTestService tests individual pipelines by temporarily rewiring them
// to GitHub, running a build, restoring the pipeline, and monitoring build progress.
type PipelineTestService struct {
	api          pipelineTestAdoAPI
	rewirer      pipelineRewirer
	log          *logger.Logger
	pollInterval time.Duration
}

// NewPipelineTestService creates a new PipelineTestService.
func NewPipelineTestService(api pipelineTestAdoAPI, rewirer pipelineRewirer, log *logger.Logger) *PipelineTestService {
	return &PipelineTestService{
		api:          api,
		rewirer:      rewirer,
		log:          log,
		pollInterval: 30 * time.Second,
	}
}

// TestPipeline tests a single pipeline by temporarily rewiring it to GitHub,
// running a build, and restoring it. Returns a PipelineTestResult.
func (s *PipelineTestService) TestPipeline(ctx context.Context, args PipelineTestArgs) (PipelineTestResult, error) {
	pipelineId := 0
	if args.PipelineId != nil {
		pipelineId = *args.PipelineId
	}

	result := PipelineTestResult{
		AdoOrg:         args.AdoOrg,
		AdoTeamProject: args.AdoTeamProject,
		PipelineName:   args.PipelineName,
		PipelineId:     pipelineId,
		StartTime:      time.Now().UTC(),
		PipelineUrl:    fmt.Sprintf("https://dev.azure.com/%s/%s/_build/definition?definitionId=%d", args.AdoOrg, args.AdoTeamProject, pipelineId),
	}

	// Track original config for restoration
	var originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules string
	var originalTriggers json.RawMessage

	err := s.runTest(ctx, &args, &result, &originalRepoName, &originalDefaultBranch, &originalClean, &originalCheckoutSubmodules, &originalTriggers)
	if err != nil {
		// Check if it's already a UserError — if so, return as-is
		var userErr *cmdutil.UserError
		if errors.As(err, &userErr) {
			return result, err
		}

		result.ErrorMessage = err.Error()
		now := time.Now().UTC()
		result.EndTime = &now

		// Attempt emergency restoration if pipeline was rewired but not yet restored
		if originalRepoName != "" && result.RewiredSuccessfully && !result.RestoredSuccessfully {
			s.attemptEmergencyRestore(ctx, args, result.PipelineId, originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers, &result)
		}

		return result, cmdutil.WrapUserError(
			fmt.Sprintf("Failed to test pipeline '%s': %s", args.PipelineName, err.Error()),
			err,
		)
	}

	now := time.Now().UTC()
	result.EndTime = &now
	return result, nil
}

func (s *PipelineTestService) runTest(
	ctx context.Context,
	args *PipelineTestArgs,
	result *PipelineTestResult,
	originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules *string,
	originalTriggers *json.RawMessage,
) error {
	// Step 1: Resolve pipeline ID if not provided
	if args.PipelineId == nil {
		id, err := s.api.GetPipelineId(ctx, args.AdoOrg, args.AdoTeamProject, args.PipelineName)
		if err != nil {
			return err
		}
		args.PipelineId = &id
		result.PipelineId = id
		result.PipelineUrl = fmt.Sprintf("https://dev.azure.com/%s/%s/_build/definition?definitionId=%d", args.AdoOrg, args.AdoTeamProject, id)
	}

	// Step 2: Check if pipeline is enabled
	isEnabled, err := s.api.IsPipelineEnabled(ctx, args.AdoOrg, args.AdoTeamProject, *args.PipelineId)
	if err != nil {
		return err
	}
	if !isEnabled {
		s.log.Warning("Pipeline '%s' (ID: %d) is disabled. Skipping pipeline test.", args.PipelineName, *args.PipelineId)
		result.ErrorMessage = "Pipeline is disabled"
		now := time.Now().UTC()
		result.EndTime = &now
		return nil
	}

	// Step 3: Get original repository information for restoration
	pipelineRepo, err := s.api.GetPipelineRepository(ctx, args.AdoOrg, args.AdoTeamProject, *args.PipelineId)
	if err != nil {
		return err
	}
	*originalRepoName = pipelineRepo.RepoName
	*originalDefaultBranch = pipelineRepo.DefaultBranch
	*originalClean = pipelineRepo.Clean
	*originalCheckoutSubmodules = pipelineRepo.CheckoutSubmodules
	result.AdoRepoName = pipelineRepo.RepoName

	pipelineInfo, err := s.api.GetPipeline(ctx, args.AdoOrg, args.AdoTeamProject, *args.PipelineId)
	if err != nil {
		return err
	}
	*originalTriggers = pipelineInfo.Triggers

	// Step 4: Rewire to GitHub
	_, err = s.rewirer.RewirePipelineToGitHub(
		ctx, args.AdoOrg, args.AdoTeamProject, *args.PipelineId,
		pipelineInfo.DefaultBranch, pipelineInfo.Clean, pipelineInfo.CheckoutSubmodules,
		args.GithubOrg, args.GithubRepo, args.ServiceConnectionId,
		pipelineInfo.Triggers, args.TargetApiUrl,
	)
	if err != nil {
		return err
	}
	result.RewiredSuccessfully = true

	// Step 5: Queue a build
	buildId, err := s.api.QueueBuild(ctx, args.AdoOrg, args.AdoTeamProject, *args.PipelineId, fmt.Sprintf("refs/heads/%s", pipelineInfo.DefaultBranch))
	if err != nil {
		return err
	}
	result.BuildId = buildId

	buildStatus, err := s.api.GetBuildStatus(ctx, args.AdoOrg, args.AdoTeamProject, buildId)
	if err != nil {
		return err
	}
	result.BuildUrl = buildStatus.URL

	// Step 6: Restore to ADO immediately after queuing build
	restoreErr := s.api.RestorePipelineToAdoRepo(
		ctx, args.AdoOrg, args.AdoTeamProject, *args.PipelineId,
		*originalRepoName, *originalDefaultBranch, *originalClean, *originalCheckoutSubmodules,
		*originalTriggers,
	)
	if restoreErr != nil {
		var userErr *cmdutil.UserError
		if errors.As(restoreErr, &userErr) {
			return restoreErr
		}
		result.ErrorMessage = fmt.Sprintf("Failed to restore: %s", restoreErr.Error())
		result.RestoredSuccessfully = false
		s.log.Errorf("Failed to restore pipeline %s: %s", args.PipelineName, restoreErr.Error())
	} else {
		result.RestoredSuccessfully = true
	}

	// Step 7: Monitor build progress
	finalStatus, finalResult := s.monitorBuildProgress(ctx, args.AdoOrg, args.AdoTeamProject, buildId, args.MonitorTimeoutMinutes, args.PipelineName)
	result.Status = finalStatus
	result.Result = finalResult

	return nil
}

func (s *PipelineTestService) monitorBuildProgress(
	ctx context.Context,
	org, teamProject string,
	buildId, timeoutMinutes int,
	pipelineName string,
) (string, string) {
	timeout := time.Duration(timeoutMinutes) * time.Minute
	startTime := time.Now()

	for time.Since(startTime) < timeout {
		buildStatus, err := s.api.GetBuildStatus(ctx, org, teamProject, buildId)
		if err != nil {
			s.log.Warning("Error checking build status: %v", err)
			break
		}

		if buildStatus.Result != "" {
			return buildStatus.Status, buildStatus.Result
		}

		s.log.Info("%s: Still waiting on pipeline '%s' (Build ID: %d)",
			time.Now().UTC().Format("2006-01-02 15:04:05"), pipelineName, buildId)

		select {
		case <-ctx.Done():
			return "timedOut", ""
		case <-time.After(s.pollInterval):
		}
	}

	return "timedOut", ""
}

func (s *PipelineTestService) attemptEmergencyRestore(
	ctx context.Context,
	args PipelineTestArgs,
	pipelineId int,
	originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules string,
	originalTriggers json.RawMessage,
	result *PipelineTestResult,
) {
	restoreErr := s.api.RestorePipelineToAdoRepo(
		ctx, args.AdoOrg, args.AdoTeamProject, pipelineId,
		originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules,
		originalTriggers,
	)
	if restoreErr != nil {
		result.RestoredSuccessfully = false
		s.log.Errorf("MANUAL RESTORATION REQUIRED for pipeline %s (ID: %d)", args.PipelineName, pipelineId)
	} else {
		result.RestoredSuccessfully = true
	}
}
