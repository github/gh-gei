package main

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/alerts"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// codeScanningMigrator is the consumer-defined interface for migrating code scanning alerts.
type codeScanningMigrator interface {
	MigrateCodeScanningAlerts(ctx context.Context, sourceOrg, sourceRepo, targetOrg, targetRepo string, dryRun bool) error
}

// newMigrateCodeScanningCmd creates the migrate-code-scanning-alerts cobra command.
func newMigrateCodeScanningCmd(svc codeScanningMigrator, log *logger.Logger) *cobra.Command {
	var (
		sourceOrg    string
		sourceRepo   string
		targetOrg    string
		targetRepo   string
		targetAPIURL string
		ghesAPIURL   string
		noSSLVerify  bool
		githubSrcPAT string
		githubTgtPAT string
		dryRun       bool
	)

	cmd := &cobra.Command{
		Use:   "migrate-code-scanning-alerts",
		Short: "Migrates code-scanning analyses, alert states, and dismissed-reasons",
		Long:  "Migrates all code-scanning analyses, alert states and possible dismissed-reasons for the default branch. This lets you migrate the history of code-scanning alerts to the target repository.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			if err := validateCodeScanningArgs(sourceOrg, sourceRepo, targetOrg, targetRepo, log); err != nil {
				return err
			}
			return runMigrateCodeScanning(cmd.Context(), svc, log, sourceOrg, sourceRepo, targetOrg, targetRepo, dryRun)
		},
	}

	cmd.Flags().StringVar(&sourceOrg, "source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&sourceRepo, "source-repo", "", "Source repository name (REQUIRED)")
	cmd.Flags().StringVar(&targetOrg, "target-org", "", "Target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&targetRepo, "target-repo", "", "Target repository name (defaults to source-repo)")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance (defaults to https://api.github.com)")
	cmd.Flags().StringVar(&ghesAPIURL, "ghes-api-url", "", "API endpoint for GHES instance")
	cmd.Flags().BoolVar(&noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for GHES")
	cmd.Flags().StringVar(&githubSrcPAT, "github-source-pat", "", "Personal access token for the source GitHub instance")
	cmd.Flags().StringVar(&githubTgtPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().BoolVar(&dryRun, "dry-run", false, "Execute in dry run mode without making actual changes")

	return cmd
}

func validateCodeScanningArgs(sourceOrg, sourceRepo, targetOrg, targetRepo string, log *logger.Logger) error {
	if err := cmdutil.ValidateRequired(sourceOrg, "--source-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(sourceRepo, "--source-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(targetOrg, "--target-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(sourceOrg, "--source-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(targetOrg, "--target-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(sourceRepo, "--source-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(targetRepo, "--target-repo"); err != nil {
		return err
	}
	return nil
}

func runMigrateCodeScanning(ctx context.Context, svc codeScanningMigrator, log *logger.Logger, sourceOrg, sourceRepo, targetOrg, targetRepo string, dryRun bool) error {
	// Default target-repo to source-repo
	if strings.TrimSpace(targetRepo) == "" {
		targetRepo = sourceRepo
		log.Info("Since target-repo is not provided, source-repo value will be used for target-repo.")
	}

	log.Info("Migrating Repo Code Scanning Alerts...")

	if err := svc.MigrateCodeScanningAlerts(ctx, sourceOrg, sourceRepo, targetOrg, targetRepo, dryRun); err != nil {
		return err
	}

	if !dryRun {
		log.Success("Code scanning alerts successfully migrated.")
	}
	return nil
}

// newMigrateCodeScanningCmdLive creates the migrate-code-scanning-alerts command with real deps.
func newMigrateCodeScanningCmdLive() *cobra.Command {
	var (
		sourceOrg    string
		sourceRepo   string
		targetOrg    string
		targetRepo   string
		targetAPIURL string
		ghesAPIURL   string
		noSSLVerify  bool
		githubSrcPAT string
		githubTgtPAT string
		dryRun       bool
	)

	cmd := &cobra.Command{
		Use:   "migrate-code-scanning-alerts",
		Short: "Migrates code-scanning analyses, alert states, and dismissed-reasons",
		Long:  "Migrates all code-scanning analyses, alert states and possible dismissed-reasons for the default branch. This lets you migrate the history of code-scanning alerts to the target repository.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			ctx := cmd.Context()
			envProv := env.New()

			if err := validateCodeScanningArgs(sourceOrg, sourceRepo, targetOrg, targetRepo, log); err != nil {
				return err
			}

			// Resolve tokens from flags or environment
			sourcePAT := resolveAlertSourceToken(githubSrcPAT, githubTgtPAT, envProv)
			targetPAT := resolveAlertTargetToken(githubTgtPAT, envProv)

			// Build source client
			sourceAPIURL := ghesAPIURL
			if sourceAPIURL == "" {
				sourceAPIURL = defaultGitHubAPIURL
			}
			sourceOpts := []github.Option{
				github.WithAPIURL(sourceAPIURL),
				github.WithLogger(log),
			}
			if noSSLVerify {
				sourceOpts = append(sourceOpts, github.WithNoSSLVerify())
			}
			sourceGH := github.NewClient(sourcePAT, sourceOpts...)

			// Build target client
			tgtAPI := targetAPIURL
			if tgtAPI == "" {
				tgtAPI = defaultGitHubAPIURL
			}
			targetGH := github.NewClient(targetPAT,
				github.WithAPIURL(tgtAPI),
				github.WithLogger(log),
			)

			svc := alerts.NewCodeScanningService(sourceGH, targetGH, log)
			return runMigrateCodeScanning(ctx, svc, log, sourceOrg, sourceRepo, targetOrg, targetRepo, dryRun)
		},
	}

	cmd.Flags().StringVar(&sourceOrg, "source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&sourceRepo, "source-repo", "", "Source repository name (REQUIRED)")
	cmd.Flags().StringVar(&targetOrg, "target-org", "", "Target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&targetRepo, "target-repo", "", "Target repository name (defaults to source-repo)")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance (defaults to https://api.github.com)")
	cmd.Flags().StringVar(&ghesAPIURL, "ghes-api-url", "", "API endpoint for GHES instance")
	cmd.Flags().BoolVar(&noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for GHES")
	cmd.Flags().StringVar(&githubSrcPAT, "github-source-pat", "", "Personal access token for the source GitHub instance")
	cmd.Flags().StringVar(&githubTgtPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().BoolVar(&dryRun, "dry-run", false, "Execute in dry run mode without making actual changes")

	return cmd
}
