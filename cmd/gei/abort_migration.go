package main

import (
	"context"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migrationAborter is the consumer-defined interface for aborting migrations.
type migrationAborter = sharedcmd.MigrationAborter

// newAbortMigrationCmd creates the abort-migration cobra command.
func newAbortMigrationCmd(gh migrationAborter, log *logger.Logger) *cobra.Command {
	var migrationID string

	cmd := &cobra.Command{
		Use:   "abort-migration",
		Short: "Aborts a repository migration that is queued or in progress",
		Long:  "Aborts a repository migration that is queued or in progress.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := sharedcmd.ValidateAbortMigrationID(migrationID); err != nil {
				return err
			}
			return sharedcmd.RunAbortMigration(cmd.Context(), gh, log, migrationID)
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "",
		"The ID of the migration to abort, starting with RM_. Organization migrations, where the ID starts with OM_, are not supported.")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// validateAbortMigrationID delegates to sharedcmd for backward compat with tests.
func validateAbortMigrationID(id string) error {
	return sharedcmd.ValidateAbortMigrationID(id)
}

// runAbortMigration delegates to sharedcmd for backward compat with tests.
func runAbortMigration(ctx context.Context, gh migrationAborter, log *logger.Logger, migrationID string) error {
	return sharedcmd.RunAbortMigration(ctx, gh, log, migrationID)
}
