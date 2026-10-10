package sharedcmd

import (
	"context"

	"github.com/github/gh-gei/pkg/logger"
)

// MigratorRoleRevoker is the consumer-defined interface for revoking migrator roles.
type MigratorRoleRevoker interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	RevokeMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error)
}

func RunRevokeMigratorRole(ctx context.Context, gh MigratorRoleRevoker, log *logger.Logger, githubOrg, actor, actorType string) error {
	log.Info("Revoking migrator role ...")

	orgID, err := gh.GetOrganizationId(ctx, githubOrg)
	if err != nil {
		return err
	}

	success, err := gh.RevokeMigratorRole(ctx, orgID, actor, actorType)
	if err != nil {
		return err
	}

	if success {
		log.Success("Migrator role successfully revoked for the %s \"%s\"", actorType, actor)
	} else {
		log.Errorf("Migrator role couldn't be revoked for the %s \"%s\"", actorType, actor)
	}

	return nil
}
