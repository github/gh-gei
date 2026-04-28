package main

import (
	"context"
	"fmt"
	"net/url"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/migration"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const (
	adoMigrationPollIntervalDefault = 60 * time.Second
	defaultAdoServerURL             = "https://dev.azure.com"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// adoMigrateRepoGitHub defines the GitHub API methods needed by migrate-repo.
type adoMigrateRepoGitHub interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	CreateAdoMigrationSource(ctx context.Context, orgID, adoServerURL string) (string, error)
	StartMigration(ctx context.Context, migrationSourceID, sourceRepoURL, orgID, repo, sourceToken, targetToken string, opts ...github.StartMigrationOption) (string, error)
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
}

// adoMigrateRepoEnvProvider provides environment variable fallbacks.
type adoMigrateRepoEnvProvider interface {
	TargetGitHubPAT() string
	ADOPAT() string
}

// ---------------------------------------------------------------------------
// Options (configurable for testing)
// ---------------------------------------------------------------------------

type adoMigrateRepoOptions struct {
	pollInterval time.Duration
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type adoMigrateRepoArgs struct {
	adoOrg               string
	adoTeamProject       string
	adoRepo              string
	githubOrg            string
	githubRepo           string
	adoServerURL         string
	queueOnly            bool
	targetRepoVisibility string
	targetAPIURL         string
	adoPAT               string
	githubPAT            string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newMigrateRepoCmd(
	gh adoMigrateRepoGitHub,
	envProv adoMigrateRepoEnvProvider,
	log *logger.Logger,
	opts adoMigrateRepoOptions,
) *cobra.Command {
	var a adoMigrateRepoArgs

	cmd := &cobra.Command{
		Use:   "migrate-repo",
		Short: "Migrates an Azure DevOps repository to GitHub",
		Long:  "Migrates a repository from Azure DevOps to GitHub.com using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runAdoMigrateRepo(cmd.Context(), gh, envProv, log, opts, a)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoRepo, "ado-repo", "", "Azure DevOps repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "Target GitHub repository name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.adoServerURL, "ado-server-url", "", "Azure DevOps Server URL (defaults to https://dev.azure.com)")
	cmd.Flags().BoolVar(&a.queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().StringVar(&a.targetRepoVisibility, "target-repo-visibility", "", "Target repository visibility (public, private, internal)")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("ado-server-url")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newMigrateRepoCmdLive() *cobra.Command {
	var a adoMigrateRepoArgs

	cmd := &cobra.Command{
		Use:   "migrate-repo",
		Short: "Migrates an Azure DevOps repository to GitHub",
		Long:  "Migrates a repository from Azure DevOps to GitHub.com using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &adoEnvProviderAdapter{prov: env.New()}

			// Resolve tokens for client construction
			githubPAT := a.githubPAT
			if githubPAT == "" {
				githubPAT = envProv.TargetGitHubPAT()
			}

			apiURL := a.targetAPIURL
			if apiURL == "" {
				apiURL = "https://api.github.com"
			}

			gh := github.NewClient(githubPAT,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			opts := adoMigrateRepoOptions{
				pollInterval: adoMigrationPollIntervalDefault,
			}

			return runAdoMigrateRepo(cmd.Context(), gh, envProv, log, opts, a)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoRepo, "ado-repo", "", "Azure DevOps repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "Target GitHub repository name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.adoServerURL, "ado-server-url", "", "Azure DevOps Server URL (defaults to https://dev.azure.com)")
	cmd.Flags().BoolVar(&a.queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().StringVar(&a.targetRepoVisibility, "target-repo-visibility", "", "Target repository visibility (public, private, internal)")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("ado-server-url")

	return cmd
}

// adoEnvProviderAdapter wraps env.Provider to satisfy adoMigrateRepoEnvProvider.
type adoEnvProviderAdapter struct {
	prov *env.Provider
}

func (a *adoEnvProviderAdapter) TargetGitHubPAT() string { return a.prov.TargetGitHubPAT() }
func (a *adoEnvProviderAdapter) ADOPAT() string          { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateAdoMigrateRepoArgs(a *adoMigrateRepoArgs) error {
	if err := cmdutil.ValidateRequired(a.adoOrg, "--ado-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoTeamProject, "--ado-team-project"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoRepo, "--ado-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubOrg, "--github-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubRepo, "--github-repo"); err != nil {
		return err
	}

	// URL validation
	if err := cmdutil.ValidateNoURL(a.githubOrg, "--github-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.githubRepo, "--github-repo"); err != nil {
		return err
	}

	// Target repo visibility
	if err := cmdutil.ValidateOneOf(a.targetRepoVisibility, "--target-repo-visibility", "public", "private", "internal"); err != nil {
		return err
	}

	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runAdoMigrateRepo(
	ctx context.Context,
	gh adoMigrateRepoGitHub,
	envProv adoMigrateRepoEnvProvider,
	log *logger.Logger,
	opts adoMigrateRepoOptions,
	a adoMigrateRepoArgs,
) error {
	if err := validateAdoMigrateRepoArgs(&a); err != nil {
		return err
	}

	log.Info("Migrating Repo...")

	// Resolve tokens from flags or environment
	if a.githubPAT == "" {
		a.githubPAT = envProv.TargetGitHubPAT()
	}
	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	// Build ADO repo URL
	adoRepoURL := getAdoRepoURL(a.adoOrg, a.adoTeamProject, a.adoRepo, a.adoServerURL)

	// Get org ID
	githubOrgID, err := gh.GetOrganizationId(ctx, a.githubOrg)
	if err != nil {
		return err
	}

	// Create migration source
	migrationSourceID, err := gh.CreateAdoMigrationSource(ctx, githubOrgID, a.adoServerURL)
	if err != nil {
		if strings.Contains(err.Error(), "not have the correct permissions to execute") {
			msg := fmt.Sprintf("%s%s", err.Error(), adoInsufficientPermissionsMessage(a.githubOrg))
			return cmdutil.NewUserError(msg)
		}
		return err
	}

	// Build migration options
	var migOpts []github.StartMigrationOption
	if a.targetRepoVisibility != "" {
		migOpts = append(migOpts, github.WithTargetRepoVisibility(a.targetRepoVisibility))
	}

	// Start migration
	migrationID, err := gh.StartMigration(ctx, migrationSourceID, adoRepoURL, githubOrgID, a.githubRepo, a.adoPAT, a.githubPAT, migOpts...)
	if err != nil {
		if err.Error() == fmt.Sprintf("A repository called %s/%s already exists", a.githubOrg, a.githubRepo) {
			log.Warning("The Org '%s' already contains a repository with the name '%s'. No operation will be performed", a.githubOrg, a.githubRepo)
			return nil
		}
		return err
	}

	// Queue-only mode
	if a.queueOnly {
		log.Info("A repository migration (ID: %s) was successfully queued.", migrationID)
		return nil
	}

	return adoWaitForMigration(ctx, gh, log, opts.pollInterval, migrationID, a.githubOrg, a.githubRepo)
}

func adoWaitForMigration(
	ctx context.Context,
	gh adoMigrateRepoGitHub,
	log *logger.Logger,
	pollInterval time.Duration,
	migrationID, githubOrg, githubRepo string,
) error {
	m, err := gh.GetMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	for migration.IsRepoPending(m.State) {
		log.Info("Migration in progress (ID: %s). State: %s. Waiting %s...", migrationID, m.State, adoFormatPollInterval(pollInterval))

		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(pollInterval):
		}

		m, err = gh.GetMigration(ctx, migrationID)
		if err != nil {
			return err
		}
	}

	if migration.IsRepoFailed(m.State) {
		log.Errorf("Migration Failed. Migration ID: %s", migrationID)
		adoLogWarningsCount(log, m.WarningsCount)
		log.Info("Migration log available at %s or by running `gh ado2gh download-logs --github-org %s --github-repo %s`", m.MigrationLogURL, githubOrg, githubRepo)
		return cmdutil.NewUserError(m.FailureReason)
	}

	log.Success("Migration completed (ID: %s)! State: %s", migrationID, m.State)
	adoLogWarningsCount(log, m.WarningsCount)
	log.Info("Migration log available at %s or by running `gh ado2gh download-logs --github-org %s --github-repo %s`", m.MigrationLogURL, githubOrg, githubRepo)

	return nil
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func getAdoRepoURL(org, project, repo, serverURL string) string {
	if strings.TrimSpace(serverURL) != "" {
		serverURL = strings.TrimRight(serverURL, "/")
	} else {
		serverURL = defaultAdoServerURL
	}
	return fmt.Sprintf("%s/%s/%s/_git/%s",
		serverURL,
		url.PathEscape(org),
		url.PathEscape(project),
		url.PathEscape(repo),
	)
}

func adoInsufficientPermissionsMessage(org string) string {
	return fmt.Sprintf(". Please check that:\n  (a) you are a member of the `%s` organization,\n  (b) you are an organization owner or you have been granted the migrator role and\n  (c) your personal access token has the correct scopes.\nFor more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.", org)
}

func adoLogWarningsCount(log *logger.Logger, count int) {
	switch count {
	case 0:
		// no output
	case 1:
		log.Warning("1 warning encountered during this migration")
	default:
		log.Warning("%d warnings encountered during this migration", count)
	}
}

func adoFormatPollInterval(d time.Duration) string {
	secs := int(d.Seconds())
	if secs == 0 {
		return "0 seconds"
	}
	return fmt.Sprintf("%d seconds", secs)
}
