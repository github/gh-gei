// Package alerts provides services for migrating GitHub security alerts between repositories.
package alerts

import (
	"context"
	"fmt"
	"strings"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
)

// SecretScanningGitHubAPI defines the GitHub API methods needed by SecretScanningService.
// Consumers define the interface at point of use.
type SecretScanningGitHubAPI interface {
	GetSecretScanningAlertsForRepository(ctx context.Context, org, repo string) ([]github.SecretScanningAlert, error)
	GetSecretScanningAlertsLocations(ctx context.Context, org, repo string, alertNumber int) ([]github.SecretScanningAlertLocation, error)
	UpdateSecretScanningAlert(ctx context.Context, org, repo string, alertNumber int, state, resolution, resolutionComment string) error
}

// SecretScanningService migrates secret scanning alert states from a source to a target repository.
type SecretScanningService struct {
	source SecretScanningGitHubAPI
	target SecretScanningGitHubAPI
	log    *logger.Logger
}

// NewSecretScanningService creates a new SecretScanningService.
func NewSecretScanningService(source, target SecretScanningGitHubAPI, log *logger.Logger) *SecretScanningService {
	return &SecretScanningService{source: source, target: target, log: log}
}

// alertWithLocations bundles an alert with its locations.
type alertWithLocations struct {
	alert     github.SecretScanningAlert
	locations []github.SecretScanningAlertLocation
}

// secretKey is the dictionary key for matching alerts.
type secretKey struct {
	SecretType string
	Secret     string
}

// MigrateAlerts migrates resolved secret scanning alerts from source to target.
func (s *SecretScanningService) MigrateAlerts(ctx context.Context, sourceOrg, sourceRepo, targetOrg, targetRepo string, dryRun bool) error {
	s.log.Info("Migrating Secret Scanning Alerts from '%s/%s' to '%s/%s'", sourceOrg, sourceRepo, targetOrg, targetRepo)

	sourceDict, err := s.getAlertsWithLocations(ctx, s.source, sourceOrg, sourceRepo)
	if err != nil {
		return fmt.Errorf("failed to get source alerts: %w", err)
	}

	targetDict, err := s.getAlertsWithLocations(ctx, s.target, targetOrg, targetRepo)
	if err != nil {
		return fmt.Errorf("failed to get target alerts: %w", err)
	}

	s.log.Info("Source %s/%s secret alerts found: %d", sourceOrg, sourceRepo, countAlerts(sourceDict))
	s.log.Info("Target %s/%s secret alerts found: %d", targetOrg, targetRepo, countAlerts(targetDict))
	s.log.Info("Matching secret resolutions from source to target repository")

	for key, sourceAlerts := range sourceDict {
		for _, src := range sourceAlerts {
			s.log.Info("Processing source secret %d", src.alert.Number)

			if src.alert.IsOpen() {
				s.log.Info("  secret alert is still open, nothing to do")
				continue
			}

			s.log.Info("  secret is resolved, looking for matching secret in target...")

			potentialTargets, found := targetDict[key]
			if !found {
				s.log.Warning("  Failed to locate a matching secret to source secret %d in %s/%s", src.alert.Number, targetOrg, targetRepo)
				continue
			}

			matched := findMatchingTarget(src.locations, potentialTargets)
			if matched == nil {
				s.log.Warning("  failed to locate a matching secret to source secret %d in %s/%s", src.alert.Number, targetOrg, targetRepo)
				continue
			}

			s.log.Info("  source secret alert matched to %d in %s/%s.", matched.alert.Number, targetOrg, targetRepo)

			if src.alert.Resolution == matched.alert.Resolution && src.alert.State == matched.alert.State {
				s.log.Info("  source and target alerts are already aligned.")
				continue
			}

			if dryRun {
				s.log.Info("  executing in dry run mode! Target alert %d would have been updated to state:%s and resolution:%s",
					matched.alert.Number, src.alert.State, src.alert.Resolution)
				continue
			}

			s.log.Info("  updating target alert:%d to state:%s and resolution:%s",
				matched.alert.Number, src.alert.State, src.alert.Resolution)

			comment := buildResolutionComment(src.alert.ResolverName, src.alert.ResolutionComment)

			if err := s.target.UpdateSecretScanningAlert(ctx, targetOrg, targetRepo,
				matched.alert.Number, src.alert.State, src.alert.Resolution, comment); err != nil {
				return fmt.Errorf("failed to update target alert %d: %w", matched.alert.Number, err)
			}

			s.log.Success("  target alert successfully updated to %s with comment %s.", src.alert.Resolution, comment)
		}
	}

	return nil
}

func countAlerts(dict map[secretKey][]alertWithLocations) int {
	n := 0
	for _, v := range dict {
		n += len(v)
	}
	return n
}

// getAlertsWithLocations fetches all alerts and their locations, grouped by (SecretType, Secret).
func (s *SecretScanningService) getAlertsWithLocations(ctx context.Context, api SecretScanningGitHubAPI, org, repo string) (map[secretKey][]alertWithLocations, error) {
	alerts, err := api.GetSecretScanningAlertsForRepository(ctx, org, repo)
	if err != nil {
		return nil, err
	}

	dict := make(map[secretKey][]alertWithLocations)
	for _, a := range alerts {
		locs, err := api.GetSecretScanningAlertsLocations(ctx, org, repo, a.Number)
		if err != nil {
			return nil, err
		}
		key := secretKey{SecretType: a.SecretType, Secret: a.Secret}
		dict[key] = append(dict[key], alertWithLocations{alert: a, locations: locs})
	}
	return dict, nil
}

// findMatchingTarget finds a target alertWithLocations whose locations all match the source locations.
func findMatchingTarget(sourceLocations []github.SecretScanningAlertLocation, targets []alertWithLocations) *alertWithLocations {
	for i := range targets {
		if doAllLocationsMatch(sourceLocations, targets[i].locations) {
			return &targets[i]
		}
	}
	return nil
}

// doAllLocationsMatch returns true if every source location has a matching target location.
func doAllLocationsMatch(source, target []github.SecretScanningAlertLocation) bool {
	if len(source) != len(target) {
		return false
	}
	for _, sl := range source {
		if !isLocationMatched(sl, target) {
			return false
		}
	}
	return true
}

func isLocationMatched(source github.SecretScanningAlertLocation, targets []github.SecretScanningAlertLocation) bool {
	for _, t := range targets {
		if areLocationsEqual(source, t) {
			return true
		}
	}
	return false
}

func areLocationsEqual(a, b github.SecretScanningAlertLocation) bool {
	if a.LocationType != b.LocationType {
		return false
	}
	switch a.LocationType {
	case "commit", "wiki_commit":
		return a.Path == b.Path &&
			a.StartLine == b.StartLine &&
			a.EndLine == b.EndLine &&
			a.StartColumn == b.StartColumn &&
			a.EndColumn == b.EndColumn &&
			a.BlobSha == b.BlobSha
	default:
		return compareURLIDs(getLocationURL(a), getLocationURL(b))
	}
}

func getLocationURL(loc github.SecretScanningAlertLocation) string {
	switch loc.LocationType {
	case "issue_title":
		return loc.IssueTitleUrl
	case "issue_body":
		return loc.IssueBodyUrl
	case "issue_comment":
		return loc.IssueCommentUrl
	case "pull_request_title":
		return loc.PullRequestTitleUrl
	case "pull_request_body":
		return loc.PullRequestBodyUrl
	case "pull_request_comment":
		return loc.PullRequestCommentUrl
	case "pull_request_review":
		return loc.PullRequestReviewUrl
	case "pull_request_review_comment":
		return loc.PullRequestReviewCommentUrl
	default:
		return ""
	}
}

func compareURLIDs(sourceURL, targetURL string) bool {
	if sourceURL == "" || targetURL == "" {
		return false
	}
	return lastSegment(sourceURL) == lastSegment(targetURL)
}

func lastSegment(u string) string {
	u = strings.TrimRight(u, "/")
	idx := strings.LastIndex(u, "/")
	if idx < 0 {
		return u
	}
	return u[idx+1:]
}

// buildResolutionComment creates the comment with [@resolverName] prefix, truncated to 270 chars.
func buildResolutionComment(resolverName, originalComment string) string {
	prefix := fmt.Sprintf("[@%s] ", resolverName)
	prefixed := prefix + originalComment
	if len(prefixed) <= 270 {
		return prefixed
	}
	// Truncate the original comment, keeping the prefix
	maxOriginal := 270 - len(prefix)
	if maxOriginal < 0 {
		maxOriginal = 0
	}
	return prefix + originalComment[:maxOriginal]
}
