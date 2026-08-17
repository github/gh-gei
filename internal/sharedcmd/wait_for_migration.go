package sharedcmd

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/migration"
)

const (
	RepoMigrationIDPrefix = "RM_"
	OrgMigrationIDPrefix  = "OM_"
	DefaultPollInterval   = 60 * time.Second
)

// MigrationWaiter is the consumer-defined interface for waiting on migrations.
type MigrationWaiter interface {
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
	GetOrganizationMigration(ctx context.Context, id string) (*github.OrgMigration, error)
}

func ValidateMigrationID(id string) error {
	if strings.TrimSpace(id) == "" {
		return cmdutil.NewUserError("--migration-id must be provided")
	}
	if !strings.HasPrefix(id, RepoMigrationIDPrefix) && !strings.HasPrefix(id, OrgMigrationIDPrefix) {
		return cmdutil.NewUserErrorf("Invalid migration id: %s", id)
	}
	return nil
}

func RunWaitForMigration(ctx context.Context, gh MigrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
	if strings.HasPrefix(migrationID, RepoMigrationIDPrefix) {
		return waitForRepoMigration(ctx, gh, log, migrationID, pollInterval)
	}
	return waitForOrgMigration(ctx, gh, log, migrationID, pollInterval)
}

func waitForRepoMigration(ctx context.Context, gh MigrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
	log.Info("Waiting for migration (ID: %s) to finish...", migrationID)

	m, err := gh.GetMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	log.Info("Waiting for migration of repository %s to finish...", m.RepositoryName)

	for {
		if migration.IsRepoSucceeded(m.State) {
			log.Success("Migration %s succeeded for %s", migrationID, m.RepositoryName)
			LogWarningsCount(log, m.WarningsCount)
			log.Info("Migration log available at %s or by running `gh gei download-logs`", m.MigrationLogURL)
			return nil
		}

		if migration.IsRepoFailed(m.State) {
			log.Errorf("Migration %s failed for %s", migrationID, m.RepositoryName)
			LogWarningsCount(log, m.WarningsCount)
			log.Info("Migration log available at %s or by running `gh gei download-logs`", m.MigrationLogURL)
			return cmdutil.NewUserError(m.FailureReason)
		}

		log.Info("Migration %s for %s is %s", migrationID, m.RepositoryName, m.State)
		log.Info("Waiting %s...", FormatPollInterval(pollInterval))

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

func waitForOrgMigration(ctx context.Context, gh MigrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
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

		log.Info("Waiting %s...", FormatPollInterval(pollInterval))

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

// LogWarningsCount logs warnings encountered during migration, matching C# WarningsCountLogger.
func LogWarningsCount(log *logger.Logger, count int) {
	switch count {
	case 0:
		// no output
	case 1:
		log.Warning("1 warning encountered during this migration")
	default:
		log.Warning("%d warnings encountered during this migration", count)
	}
}

func FormatPollInterval(d time.Duration) string {
	secs := int(d.Seconds())
	if secs == 0 {
		return "0 seconds"
	}
	return fmt.Sprintf("%d seconds", secs)
}
