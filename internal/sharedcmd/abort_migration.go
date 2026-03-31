package sharedcmd

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

// MigrationAborter is the consumer-defined interface for aborting migrations.
type MigrationAborter interface {
	AbortMigration(ctx context.Context, id string) (bool, error)
}

func ValidateAbortMigrationID(id string) error {
	if strings.TrimSpace(id) == "" {
		return cmdutil.NewUserError("--migration-id must be provided")
	}
	if !strings.HasPrefix(id, RepoMigrationIDPrefix) {
		return cmdutil.NewUserErrorf(
			"Invalid migration ID: %s. Only repository migration IDs starting with RM_ are supported.", id)
	}
	return nil
}

func RunAbortMigration(ctx context.Context, gh MigrationAborter, log *logger.Logger, migrationID string) error {
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
