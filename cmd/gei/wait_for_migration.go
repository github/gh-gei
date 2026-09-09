package main

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/migration"
	"github.com/spf13/cobra"
)

const (
	repoMigrationIDPrefix = "RM_"
	orgMigrationIDPrefix  = "OM_"
	defaultPollInterval   = 60 * time.Second
)

// migrationWaiter is the consumer-defined interface for waiting on migrations.
type migrationWaiter interface {
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
	GetOrganizationMigration(ctx context.Context, id string) (*github.OrgMigration, error)
}

// newWaitForMigrationCmd creates the wait-for-migration cobra command.
// pollInterval controls how long to sleep between status polls; pass 0 in tests.
func newWaitForMigrationCmd(gh migrationWaiter, log *logger.Logger, pollInterval time.Duration) *cobra.Command {
	var migrationID string

	cmd := &cobra.Command{
		Use:   "wait-for-migration",
		Short: "Waits for a migration to finish",
		Long:  "Polls the migration status API until a repository or organization migration completes or fails.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := validateMigrationID(migrationID); err != nil {
				return err
			}
			return runWaitForMigration(cmd.Context(), gh, log, migrationID, pollInterval)
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "", "The ID of the migration to wait for (REQUIRED)")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

func validateMigrationID(id string) error {
	if strings.TrimSpace(id) == "" {
		return cmdutil.NewUserError("--migration-id must be provided")
	}
	if !strings.HasPrefix(id, repoMigrationIDPrefix) && !strings.HasPrefix(id, orgMigrationIDPrefix) {
		return cmdutil.NewUserErrorf("Invalid migration id: %s", id)
	}
	return nil
}

func runWaitForMigration(ctx context.Context, gh migrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
	if strings.HasPrefix(migrationID, repoMigrationIDPrefix) {
		return waitForRepoMigration(ctx, gh, log, migrationID, pollInterval)
	}
	return waitForOrgMigration(ctx, gh, log, migrationID, pollInterval)
}

func waitForRepoMigration(ctx context.Context, gh migrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
	log.Info("Waiting for migration (ID: %s) to finish...", migrationID)

	m, err := gh.GetMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	log.Info("Waiting for migration of repository %s to finish...", m.RepositoryName)

	for {
		if migration.IsRepoSucceeded(m.State) {
			log.Success("Migration %s succeeded for %s", migrationID, m.RepositoryName)
			logWarningsCount(log, m.WarningsCount)
			log.Info("Migration log available at %s or by running `gh gei download-logs`", m.MigrationLogURL)
			return nil
		}

		if migration.IsRepoFailed(m.State) {
			log.Errorf("Migration %s failed for %s", migrationID, m.RepositoryName)
			logWarningsCount(log, m.WarningsCount)
			log.Info("Migration log available at %s or by running `gh gei download-logs`", m.MigrationLogURL)
			return cmdutil.NewUserError(m.FailureReason)
		}

		log.Info("Migration %s for %s is %s", migrationID, m.RepositoryName, m.State)
		log.Info("Waiting %s...", formatPollInterval(pollInterval))

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
}

func waitForOrgMigration(ctx context.Context, gh migrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
	m, err := gh.GetOrganizationMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	log.Info("Waiting for %s -> %s migration (ID: %s) to finish...", m.SourceOrgURL, m.TargetOrgName, migrationID)

	for {
		if migration.IsOrgSucceeded(m.State) {
			log.Success("Migration %s succeeded", migrationID)
			return nil
		}

		if migration.IsOrgFailed(m.State) {
			return cmdutil.NewUserErrorf("Migration %s failed for %s -> %s. Failure reason: %s",
				migrationID, m.SourceOrgURL, m.TargetOrgName, m.FailureReason)
		}

		if migration.IsOrgRepoMigration(m.State) {
			completed := m.TotalRepositoriesCount - m.RemainingRepositoriesCount
			log.Info("Migration %s is %s - %d/%d repositories completed",
				migrationID, m.State, completed, m.TotalRepositoriesCount)
		} else {
			log.Info("Migration %s is %s", migrationID, m.State)
		}

		log.Info("Waiting %s...", formatPollInterval(pollInterval))

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
}

// logWarningsCount logs warnings encountered during migration, matching C# WarningsCountLogger.
func logWarningsCount(log *logger.Logger, count int) {
	switch count {
	case 0:
		// no output
	case 1:
		log.Warning("1 warning encountered during this migration")
	default:
		log.Warning("%d warnings encountered during this migration", count)
	}
}

func formatPollInterval(d time.Duration) string {
	secs := int(d.Seconds())
	if secs == 0 {
		return "0 seconds"
	}
	return fmt.Sprintf("%d seconds", secs)
}
