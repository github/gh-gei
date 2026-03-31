package main

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migratorRoleGranter is the consumer-defined interface for granting migrator roles.
type migratorRoleGranter = sharedcmd.MigratorRoleGranter

// newGrantMigratorRoleCmd creates the grant-migrator-role cobra command.
func newGrantMigratorRoleCmd(gh migratorRoleGranter, log *logger.Logger) *cobra.Command {
	var (
		githubOrg string
		actor     string
		actorType string
	)

	cmd := &cobra.Command{
		Use:   "grant-migrator-role",
		Short: "Grants the migrator role to a user or team for a GitHub organization",
		Long:  "Grants the migrator role to a user or team for a GitHub organization.",
		RunE: func(cmd *cobra.Command, args []string) error {
			ghesAPIURL, _ := cmd.Flags().GetString("ghes-api-url")
			targetAPIURL, _ := cmd.Flags().GetString("target-api-url")
			if err := sharedcmd.ValidateMigratorRoleArgs(githubOrg, actor, actorType, ghesAPIURL, targetAPIURL); err != nil {
				return err
			}
			actorType = strings.ToUpper(actorType)
			return sharedcmd.RunGrantMigratorRole(cmd.Context(), gh, log, githubOrg, actor, actorType)
		},
	}

	cmd.Flags().StringVar(&githubOrg, "github-org", "", "The GitHub organization to grant the migrator role for (REQUIRED)")
	cmd.Flags().StringVar(&actor, "actor", "", "The user or team to grant the migrator role to (REQUIRED)")
	cmd.Flags().StringVar(&actorType, "actor-type", "", "The type of the actor (USER or TEAM) (REQUIRED)")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().String("ghes-api-url", "", "API URL for the source GHES instance")

	return cmd
}

// runGrantMigratorRole delegates to sharedcmd for backward compat with tests.
func runGrantMigratorRole(ctx context.Context, gh migratorRoleGranter, log *logger.Logger, githubOrg, actor, actorType string) error {
	return sharedcmd.RunGrantMigratorRole(ctx, gh, log, githubOrg, actor, actorType)
}
