package main

import (
	"context"
	"fmt"
	"net/url"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/migration"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interface
// ---------------------------------------------------------------------------

// orgMigrator defines the GitHub API methods needed for org migration.
type orgMigrator interface {
	GetEnterpriseId(ctx context.Context, enterpriseName string) (string, error)
	StartOrganizationMigration(ctx context.Context, sourceOrgURL, targetOrgName, targetEnterpriseID, sourceAccessToken string) (string, error)
	GetOrganizationMigration(ctx context.Context, migrationID string) (*github.OrgMigration, error)
}

// migrateOrgEnvProvider provides environment variable fallbacks for org migration.
type migrateOrgEnvProvider interface {
	SourceGitHubPAT() string
	TargetGitHubPAT() string
}

// ---------------------------------------------------------------------------
// Args
// ---------------------------------------------------------------------------

type migrateOrgArgs struct {
	githubSourceOrg        string
	githubTargetOrg        string
	githubTargetEnterprise string
	githubSourcePAT        string
	githubTargetPAT        string
	queueOnly              bool
	targetAPIURL           string
}

// ---------------------------------------------------------------------------
// Command constructor
// ---------------------------------------------------------------------------

func newMigrateOrgCmd(gh orgMigrator, envProv migrateOrgEnvProvider, log *logger.Logger, pollInterval time.Duration) *cobra.Command {
	var (
		githubSourceOrg        string
		githubTargetOrg        string
		githubTargetEnterprise string
		githubSourcePAT        string
		githubTargetPAT        string
		queueOnly              bool
		targetAPIURL           string
	)

	cmd := &cobra.Command{
		Use:   "migrate-org",
		Short: "Migrates a GitHub organization to a target enterprise",
		Long:  "Migrates a GitHub organization to a target enterprise using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runMigrateOrg(cmd.Context(), migrateOrgArgs{
				githubSourceOrg:        githubSourceOrg,
				githubTargetOrg:        githubTargetOrg,
				githubTargetEnterprise: githubTargetEnterprise,
				githubSourcePAT:        githubSourcePAT,
				githubTargetPAT:        githubTargetPAT,
				queueOnly:              queueOnly,
				targetAPIURL:           targetAPIURL,
			}, gh, envProv, log, pollInterval)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&githubSourceOrg, "github-source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "Target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetEnterprise, "github-target-enterprise", "", "Target GitHub Enterprise (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&githubSourcePAT, "github-source-pat", "", "Personal access token for the source GitHub instance")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().BoolVar(&queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateMigrateOrgArgs(a *migrateOrgArgs, log *logger.Logger) error {
	if err := cmdutil.ValidateRequired(a.githubSourceOrg, "--github-source-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubTargetOrg, "--github-target-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubTargetEnterprise, "--github-target-enterprise"); err != nil {
		return err
	}

	if err := cmdutil.ValidateNoURL(a.githubSourceOrg, "--github-source-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.githubTargetOrg, "--github-target-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.githubTargetEnterprise, "--github-target-enterprise"); err != nil {
		return err
	}

	// If target PAT is provided but source PAT is not, use target PAT as source PAT
	if a.githubTargetPAT != "" && a.githubSourcePAT == "" {
		a.githubSourcePAT = a.githubTargetPAT
		log.Info("Since github-target-pat is provided, github-source-pat will also use its value.")
	}

	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runMigrateOrg(
	ctx context.Context,
	a migrateOrgArgs,
	gh orgMigrator,
	envProv migrateOrgEnvProvider,
	log *logger.Logger,
	pollInterval time.Duration,
) error {
	if err := validateMigrateOrgArgs(&a, log); err != nil {
		return err
	}

	log.Info("Migrating Org...")

	githubEnterpriseID, err := gh.GetEnterpriseId(ctx, a.githubTargetEnterprise)
	if err != nil {
		return err
	}

	sourceOrgURL := fmt.Sprintf("%s/%s", defaultGitHubBaseURL, url.PathEscape(a.githubSourceOrg))

	sourceToken := a.githubSourcePAT
	if sourceToken == "" {
		sourceToken = envProv.SourceGitHubPAT()
	}

	migrationID, err := gh.StartOrganizationMigration(ctx, sourceOrgURL, a.githubTargetOrg, githubEnterpriseID, sourceToken)
	if err != nil {
		return err
	}

	if a.queueOnly {
		log.Info("A organization migration (ID: %s) was successfully queued.", migrationID)
		return nil
	}

	m, err := gh.GetOrganizationMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	for migration.IsOrgPending(m.State) {
		if migration.IsOrgRepoMigration(m.State) {
			migratedCount := m.TotalRepositoriesCount - m.RemainingRepositoriesCount
			log.Info("Migration in progress (ID: %s). State: %s. %d/%d repo(s) migrated. Waiting %s...",
				migrationID, m.State, migratedCount, m.TotalRepositoriesCount, formatPollInterval(pollInterval))
		} else {
			log.Info("Migration in progress (ID: %s). State: %s. Waiting %s...",
				migrationID, m.State, formatPollInterval(pollInterval))
		}

		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(pollInterval):
		}

		m, err = gh.GetOrganizationMigration(ctx, migrationID)
		if err != nil {
			return err
		}
	}

	if migration.IsOrgFailed(m.State) {
		log.Errorf("Migration Failed. Migration ID: %s", migrationID)
		return cmdutil.NewUserError(m.FailureReason)
	}

	log.Success("Migration completed (ID: %s)! State: %s", migrationID, m.State)
	return nil
}

// ---------------------------------------------------------------------------
// Production command constructor (used by main.go)
// ---------------------------------------------------------------------------

func newMigrateOrgCmdLive() *cobra.Command {
	var (
		githubSourceOrg        string
		githubTargetOrg        string
		githubTargetEnterprise string
		githubSourcePAT        string
		githubTargetPAT        string
		queueOnly              bool
		targetAPIURL           string
	)

	cmd := &cobra.Command{
		Use:   "migrate-org",
		Short: "Migrates a GitHub organization to a target enterprise",
		Long:  "Migrates a GitHub organization to a target enterprise using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			ctx := cmd.Context()
			envProv := &envProviderAdapter{prov: getEnvProvider()}

			a := migrateOrgArgs{
				githubSourceOrg:        githubSourceOrg,
				githubTargetOrg:        githubTargetOrg,
				githubTargetEnterprise: githubTargetEnterprise,
				githubSourcePAT:        githubSourcePAT,
				githubTargetPAT:        githubTargetPAT,
				queueOnly:              queueOnly,
				targetAPIURL:           targetAPIURL,
			}

			if err := validateMigrateOrgArgs(&a, log); err != nil {
				return err
			}

			// Resolve target token for client construction
			targetToken := a.githubTargetPAT
			if targetToken == "" {
				targetToken = envProv.TargetGitHubPAT()
			}

			tgtAPI := a.targetAPIURL
			if tgtAPI == "" {
				tgtAPI = defaultGitHubAPIURL
			}

			gh := github.NewClient(targetToken,
				github.WithAPIURL(tgtAPI),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			return runMigrateOrg(ctx, a, gh, envProv, log, defaultPollInterval)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&githubSourceOrg, "github-source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "Target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetEnterprise, "github-target-enterprise", "", "Target GitHub Enterprise (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&githubSourcePAT, "github-source-pat", "", "Personal access token for the source GitHub instance")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().BoolVar(&queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}
