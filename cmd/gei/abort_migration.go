package main

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migrationAborter is the consumer-defined interface for aborting migrations.
type migrationAborter interface {
	AbortMigration(ctx context.Context, id string) (bool, error)
}

// newAbortMigrationCmd creates the abort-migration cobra command.
func newAbortMigrationCmd(gh migrationAborter, log *logger.Logger) *cobra.Command {
	var migrationID string

	cmd := &cobra.Command{
		Use:   "abort-migration",
		Short: "Aborts a repository migration that is queued or in progress",
		Long:  "Aborts a repository migration that is queued or in progress.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := validateAbortMigrationID(migrationID); err != nil {
				return err
			}
			return runAbortMigration(cmd.Context(), gh, log, migrationID)
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "",
		"The ID of the migration to abort, starting with RM_. Organization migrations, where the ID starts with OM_, are not supported.")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

func validateAbortMigrationID(id string) error {
	if strings.TrimSpace(id) == "" {
		return cmdutil.NewUserError("--migration-id must be provided")
	}
	if !strings.HasPrefix(id, repoMigrationIDPrefix) {
		return cmdutil.NewUserErrorf(
			"Invalid migration ID: %s. Only repository migration IDs starting with RM_ are supported.", id)
	}
	return nil
}

func runAbortMigration(ctx context.Context, gh migrationAborter, log *logger.Logger, migrationID string) error {
	success, err := gh.AbortMigration(ctx, migrationID)
	if err != nil {
		return err
	}
	if !success {
		log.Errorf("Failed to abort migration %s", migrationID)
		return nil
	}
	log.Info("Migration %s was canceled", migrationID)
	return nil
}
