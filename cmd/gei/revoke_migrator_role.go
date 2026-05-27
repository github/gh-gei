package main

import (
	"context"
	"strings"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migratorRoleRevoker is the consumer-defined interface for revoking migrator roles.
type migratorRoleRevoker interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	RevokeMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error)
}

// newRevokeMigratorRoleCmd creates the revoke-migrator-role cobra command.
func newRevokeMigratorRoleCmd(gh migratorRoleRevoker, log *logger.Logger) *cobra.Command {
	var (
		githubOrg string
		actor     string
		actorType string
	)

	cmd := &cobra.Command{
		Use:   "revoke-migrator-role",
		Short: "Revokes the migrator role from a user or team for a GitHub organization",
		Long:  "Revokes the migrator role from a user or team for a GitHub organization.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := validateMigratorRoleArgs(githubOrg, actor, actorType, cmd); err != nil {
				return err
			}
			actorType = strings.ToUpper(actorType)
			return runRevokeMigratorRole(cmd.Context(), gh, log, githubOrg, actor, actorType)
		},
	}

	cmd.Flags().StringVar(&githubOrg, "github-org", "", "The GitHub organization to revoke the migrator role for (REQUIRED)")
	cmd.Flags().StringVar(&actor, "actor", "", "The user or team to revoke the migrator role from (REQUIRED)")
	cmd.Flags().StringVar(&actorType, "actor-type", "", "The type of the actor (USER or TEAM) (REQUIRED)")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().String("ghes-api-url", "", "API URL for the source GHES instance")

	return cmd
}

func runRevokeMigratorRole(ctx context.Context, gh migratorRoleRevoker, log *logger.Logger, githubOrg, actor, actorType string) error {
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
