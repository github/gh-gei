package alerts

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
)

// CodeScanningGitHubAPI defines the GitHub API methods needed by CodeScanningService.
type CodeScanningGitHubAPI interface {
	GetDefaultBranch(ctx context.Context, org, repo string) (string, error)
	GetCodeScanningAlertsForRepository(ctx context.Context, org, repo, branch string) ([]github.CodeScanningAlert, error)
	GetCodeScanningAlertInstances(ctx context.Context, org, repo string, alertNumber int) ([]github.CodeScanningAlertInstance, error)
	GetCodeScanningAnalysisForRepository(ctx context.Context, org, repo, branch string) ([]github.CodeScanningAnalysis, error)
	GetSarifReport(ctx context.Context, org, repo string, analysisID int) (string, error)
	UploadSarifReport(ctx context.Context, org, repo, sarif, commitSha, ref string) (string, error)
	GetSarifProcessingStatus(ctx context.Context, org, repo, sarifID string) (*github.SarifProcessingStatus, error)
	UpdateCodeScanningAlert(ctx context.Context, org, repo string, alertNumber int, state, dismissedReason, dismissedComment string) error
}

// CodeScanningService migrates code scanning analyses and alerts from source to target.
type CodeScanningService struct {
	source       CodeScanningGitHubAPI
	target       CodeScanningGitHubAPI
	log          *logger.Logger
	initialDelay time.Duration
	pollDelay    time.Duration
}

// CodeScanningOption configures a CodeScanningService.
type CodeScanningOption func(*CodeScanningService)

// WithInitialDelay sets the delay before first polling SARIF status.
func WithInitialDelay(d time.Duration) CodeScanningOption {
	return func(s *CodeScanningService) { s.initialDelay = d }
}

// WithPollDelay sets the delay between SARIF processing status polls.
func WithPollDelay(d time.Duration) CodeScanningOption {
	return func(s *CodeScanningService) { s.pollDelay = d }
}

// NewCodeScanningService creates a new CodeScanningService.
func NewCodeScanningService(source, target CodeScanningGitHubAPI, log *logger.Logger, opts ...CodeScanningOption) *CodeScanningService {
	svc := &CodeScanningService{
		source:       source,
		target:       target,
		log:          log,
		initialDelay: 500 * time.Millisecond,
		pollDelay:    5 * time.Second,
	}
	for _, opt := range opts {
		opt(svc)
	}
	return svc
}

// MigrateCodeScanningAlerts orchestrates the full migration: analyses then alerts.
func (s *CodeScanningService) MigrateCodeScanningAlerts(ctx context.Context, sourceOrg, sourceRepo, targetOrg, targetRepo string, dryRun bool) error {
	defaultBranch, err := s.source.GetDefaultBranch(ctx, sourceOrg, sourceRepo)
	if err != nil {
		return fmt.Errorf("failed to get default branch: %w", err)
	}
	s.log.Info("Found default branch: %s - migrating code scanning alerts only of this branch.", defaultBranch)

	if err := s.MigrateAnalyses(ctx, sourceOrg, sourceRepo, targetOrg, targetRepo, defaultBranch, dryRun); err != nil {
		return err
	}
	return s.MigrateAlerts(ctx, sourceOrg, sourceRepo, targetOrg, targetRepo, defaultBranch, dryRun)
}

// MigrateAnalyses downloads SARIF reports from source and uploads to target.
//
//nolint:gocyclo // Migration orchestration requires sequential validation steps
func (s *CodeScanningService) MigrateAnalyses(ctx context.Context, sourceOrg, sourceRepo, targetOrg, targetRepo, branch string, dryRun bool) error {
	s.log.Info("Migrating Code Scanning Analyses from '%s/%s' to '%s/%s'", sourceOrg, sourceRepo, targetOrg, targetRepo)

	sourceAnalyses, err := s.source.GetCodeScanningAnalysisForRepository(ctx, sourceOrg, sourceRepo, branch)
	if err != nil {
		return fmt.Errorf("failed to get source analyses: %w", err)
	}

	targetAnalyses, err := s.target.GetCodeScanningAnalysisForRepository(ctx, targetOrg, targetRepo, branch)
	if err != nil {
		return fmt.Errorf("failed to get target analyses: %w", err)
	}

	// Skip analyses that already exist on target (by count)
	relevantAnalyses := sourceAnalyses
	if len(targetAnalyses) > 0 && len(targetAnalyses) < len(sourceAnalyses) {
		s.log.Info("Already found %d analyses on target - so %d of %d source analyses will be skipped.",
			len(targetAnalyses), len(targetAnalyses), len(sourceAnalyses))
		relevantAnalyses = sourceAnalyses[len(targetAnalyses):]
	} else if len(targetAnalyses) >= len(sourceAnalyses) {
		relevantAnalyses = nil
	}

	s.log.Info("Found %d analyses to migrate.", len(relevantAnalyses))

	if dryRun {
		s.log.Info("Running in dry-run mode. The following Sarif-Reports would now be downloaded from '%s/%s' and then uploaded to '%s/%s':",
			sourceOrg, sourceRepo, targetOrg, targetRepo)
		for _, analysis := range relevantAnalyses {
			s.log.Info("    Report of Analysis with Id '%d' created at %s.", analysis.ID, analysis.CreatedAt)
		}
		return nil
	}

	for i, analysis := range relevantAnalyses {
		analysisNumber := i + 1

		sarifReport, err := s.source.GetSarifReport(ctx, sourceOrg, sourceRepo, analysis.ID)
		if err != nil {
			// Skip if SARIF not found (mirror C# behavior for 404)
			s.log.Warning("Skipping analysis %d because no analysis was found for it (%d / %d)...",
				analysis.ID, analysisNumber, len(relevantAnalyses))
			continue
		}

		s.log.Verbose("Downloaded SARIF report for analysis %d", analysis.ID)

		s.log.Info("Uploading SARIF for analysis %d in target repository (%d / %d)...",
			analysis.ID, analysisNumber, len(relevantAnalyses))

		id, err := s.target.UploadSarifReport(ctx, targetOrg, targetRepo, sarifReport, analysis.CommitSha, analysis.Ref)
		if err != nil {
			return fmt.Errorf("failed to upload SARIF for analysis %d: %w", analysis.ID, err)
		}

		// Wait before first poll
		if s.initialDelay > 0 {
			time.Sleep(s.initialDelay)
		}

		status, err := s.target.GetSarifProcessingStatus(ctx, targetOrg, targetRepo, id)
		if err != nil {
			return fmt.Errorf("failed to get SARIF processing status: %w", err)
		}

		for status.IsPending() {
			s.log.Info("   SARIF processing is still pending. Waiting...")
			if s.pollDelay > 0 {
				time.Sleep(s.pollDelay)
			}
			status, err = s.target.GetSarifProcessingStatus(ctx, targetOrg, targetRepo, id)
			if err != nil {
				return fmt.Errorf("failed to get SARIF processing status: %w", err)
			}
		}

		if status.IsFailed() {
			return fmt.Errorf("SARIF processing failed for analysis %d. Received the following Error(s): \n- %s",
				analysis.ID, strings.Join(status.Errors, "\n- "))
		}

		s.log.Info("    Successfully migrated report for analysis %d", analysis.ID)
	}

	s.log.Info("Successfully finished migrating %d Code Scanning analyses!", len(relevantAnalyses))
	return nil
}

// MigrateAlerts matches and updates code scanning alert states from source to target.
func (s *CodeScanningService) MigrateAlerts(ctx context.Context, sourceOrg, sourceRepo, targetOrg, targetRepo, branch string, dryRun bool) error {
	sourceAlerts, err := s.source.GetCodeScanningAlertsForRepository(ctx, sourceOrg, sourceRepo, branch)
	if err != nil {
		return fmt.Errorf("failed to get source alerts: %w", err)
	}

	var targetAlerts []github.CodeScanningAlert
	if !dryRun {
		targetAlerts, err = s.target.GetCodeScanningAlertsForRepository(ctx, targetOrg, targetRepo, branch)
		if err != nil {
			return fmt.Errorf("failed to get target alerts: %w", err)
		}
	}

	successCount := 0
	skippedCount := 0
	notFoundCount := 0

	s.log.Info("Found %d source and %d target alerts. Starting migration of alert states...",
		len(sourceAlerts), len(targetAlerts))

	for _, sourceAlert := range sourceAlerts {
		if !isOpenOrDismissed(sourceAlert.State) {
			s.log.Info("  skipping alert %d (%s) because state '%s' is not migratable.",
				sourceAlert.Number, sourceAlert.URL, sourceAlert.State)
			skippedCount++
			continue
		}

		if dryRun {
			s.log.Info("  running in dry-run mode. Would have tried to find target alert for %d (%s) and set state '%s'",
				sourceAlert.Number, sourceAlert.URL, sourceAlert.State)
			successCount++
			continue
		}

		matchingTarget := s.findMatchingTargetAlert(ctx, sourceOrg, sourceRepo, targetAlerts, sourceAlert)
		if matchingTarget == nil {
			s.log.Errorf("  could not find a target alert for %d (%s).", sourceAlert.Number, sourceAlert.URL)
			notFoundCount++
			continue
		}

		if matchingTarget.State == sourceAlert.State {
			s.log.Info("  skipping alert because target alert already has the same state.")
			skippedCount++
			continue
		}

		s.log.Verbose("Setting Status %s for target alert %d (%s)", sourceAlert.State, matchingTarget.Number, matchingTarget.URL)

		if err := s.target.UpdateCodeScanningAlert(ctx, targetOrg, targetRepo,
			matchingTarget.Number, sourceAlert.State, sourceAlert.DismissedReason, sourceAlert.DismissedComment); err != nil {
			return fmt.Errorf("failed to update target alert %d: %w", matchingTarget.Number, err)
		}
		successCount++
	}

	s.log.Info("Code Scanning Alerts done!\nStatus of %d Alerts:\n  Success: %d\n  Skipped (status not migratable or already matches): %d\n  No matching target found (see logs): %d.",
		len(sourceAlerts), successCount, skippedCount, notFoundCount)

	if notFoundCount > 0 {
		return fmt.Errorf("migration of code scanning alerts failed")
	}
	return nil
}

func isOpenOrDismissed(state string) bool {
	s := strings.TrimSpace(strings.ToLower(state))
	return s == "open" || s == "dismissed"
}

func (s *CodeScanningService) findMatchingTargetAlert(ctx context.Context, sourceOrg, sourceRepo string,
	targetAlerts []github.CodeScanningAlert, sourceAlert github.CodeScanningAlert,
) *github.CodeScanningAlert {
	// Filter targets with same rule ID
	var sameRule []github.CodeScanningAlert
	for _, t := range targetAlerts {
		if t.RuleId == sourceAlert.RuleId {
			sameRule = append(sameRule, t)
		}
	}

	// First: try matching by most_recent_instance
	if sourceAlert.MostRecentInstance != nil {
		for i := range sameRule {
			if sameRule[i].MostRecentInstance != nil && areInstancesEqual(*sourceAlert.MostRecentInstance, *sameRule[i].MostRecentInstance) {
				return &sameRule[i]
			}
		}
	}

	// Fallback: fetch all source instances and try matching any to any target's most_recent_instance
	allSourceInstances, err := s.source.GetCodeScanningAlertInstances(ctx, sourceOrg, sourceRepo, sourceAlert.Number)
	if err != nil {
		return nil
	}

	for i := range sameRule {
		if sameRule[i].MostRecentInstance == nil {
			continue
		}
		for _, srcInst := range allSourceInstances {
			if areInstancesEqual(srcInst, *sameRule[i].MostRecentInstance) {
				return &sameRule[i]
			}
		}
	}

	return nil
}

func areInstancesEqual(a, b github.CodeScanningAlertInstance) bool {
	return a.Ref == b.Ref &&
		a.CommitSha == b.CommitSha &&
		a.Path == b.Path &&
		a.StartLine == b.StartLine &&
		a.StartColumn == b.StartColumn &&
		a.EndLine == b.EndLine &&
		a.EndColumn == b.EndColumn
}
