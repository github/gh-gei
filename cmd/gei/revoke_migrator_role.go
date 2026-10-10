package main

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migratorRoleRevoker is the consumer-defined interface for revoking migrator roles.
type migratorRoleRevoker = sharedcmd.MigratorRoleRevoker

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
			ghesAPIURL, _ := cmd.Flags().GetString("ghes-api-url")
			targetAPIURL, _ := cmd.Flags().GetString("target-api-url")
			if err := sharedcmd.ValidateMigratorRoleArgs(githubOrg, actor, actorType, ghesAPIURL, targetAPIURL); err != nil {
				return err
			}
			actorType = strings.ToUpper(actorType)
			return sharedcmd.RunRevokeMigratorRole(cmd.Context(), gh, log, githubOrg, actor, actorType)
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

// runRevokeMigratorRole delegates to sharedcmd for backward compat with tests.
func runRevokeMigratorRole(ctx context.Context, gh migratorRoleRevoker, log *logger.Logger, githubOrg, actor, actorType string) error {
	return sharedcmd.RunRevokeMigratorRole(ctx, gh, log, githubOrg, actor, actorType)
}
