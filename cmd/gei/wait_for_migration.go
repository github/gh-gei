package main

import (
	"context"
	"time"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migrationWaiter is the consumer-defined interface for waiting on migrations.
// It matches sharedcmd.MigrationWaiter, redeclared here so tests can use local mocks.
type migrationWaiter = sharedcmd.MigrationWaiter

// Re-export constants used by other files in this package (migrate_repo.go, migrate_org.go, wiring.go).
const (
	repoMigrationIDPrefix = sharedcmd.RepoMigrationIDPrefix
	orgMigrationIDPrefix  = sharedcmd.OrgMigrationIDPrefix
	defaultPollInterval   = sharedcmd.DefaultPollInterval
)

// newWaitForMigrationCmd creates the wait-for-migration cobra command.
// pollInterval controls how long to sleep between status polls; pass 0 in tests.
func newWaitForMigrationCmd(gh migrationWaiter, log *logger.Logger, pollInterval time.Duration) *cobra.Command {
	var migrationID string

	cmd := &cobra.Command{
		Use:   "wait-for-migration",
		Short: "Waits for a migration to finish",
		Long:  "Polls the migration status API until a repository or organization migration completes or fails.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := sharedcmd.ValidateMigrationID(migrationID); err != nil {
				return err
			}
			return sharedcmd.RunWaitForMigration(cmd.Context(), gh, log, migrationID, pollInterval)
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "", "The ID of the migration to wait for (REQUIRED)")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// validateMigrationID delegates to sharedcmd for backward compatibility with tests.
func validateMigrationID(id string) error {
	return sharedcmd.ValidateMigrationID(id)
}

// runWaitForMigration delegates to sharedcmd for backward compatibility with tests.
func runWaitForMigration(ctx context.Context, gh migrationWaiter, log *logger.Logger, migrationID string, pollInterval time.Duration) error {
	return sharedcmd.RunWaitForMigration(ctx, gh, log, migrationID, pollInterval)
}

// logWarningsCount delegates to sharedcmd. Used by migrate_repo.go.
func logWarningsCount(log *logger.Logger, count int) {
	sharedcmd.LogWarningsCount(log, count)
}

// formatPollInterval delegates to sharedcmd. Used by migrate_repo.go and migrate_org.go.
func formatPollInterval(d time.Duration) string {
	return sharedcmd.FormatPollInterval(d)
}
