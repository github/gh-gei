package main

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// migratorRoleGranter is the consumer-defined interface for granting migrator roles.
type migratorRoleGranter interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	GrantMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error)
}

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
			if err := validateMigratorRoleArgs(githubOrg, actor, actorType, cmd); err != nil {
				return err
			}
			actorType = strings.ToUpper(actorType)
			return runGrantMigratorRole(cmd.Context(), gh, log, githubOrg, actor, actorType)
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

func runGrantMigratorRole(ctx context.Context, gh migratorRoleGranter, log *logger.Logger, githubOrg, actor, actorType string) error {
	log.Info("Granting migrator role ...")

	orgID, err := gh.GetOrganizationId(ctx, githubOrg)
	if err != nil {
		return err
	}

	success, err := gh.GrantMigratorRole(ctx, orgID, actor, actorType)
	if err != nil {
		return err
	}

	if success {
		log.Success("Migrator role successfully set for the %s \"%s\"", actorType, actor)
	} else {
		log.Errorf("Migrator role couldn't be set for the %s \"%s\"", actorType, actor)
	}

	return nil
}

// validateMigratorRoleArgs validates the shared arguments for grant/revoke migrator role commands.
func validateMigratorRoleArgs(githubOrg, actor, actorType string, cmd *cobra.Command) error {
	if strings.TrimSpace(githubOrg) == "" {
		return cmdutil.NewUserError("--github-org must be provided")
	}
	if strings.TrimSpace(actor) == "" {
		return cmdutil.NewUserError("--actor must be provided")
	}
	if strings.HasPrefix(githubOrg, "http://") || strings.HasPrefix(githubOrg, "https://") {
		return cmdutil.NewUserError("The --github-org option expects an organization name, not a URL. Please provide just the organization name.")
	}

	upper := strings.ToUpper(actorType)
	if upper != "TEAM" && upper != "USER" {
		return cmdutil.NewUserError("Actor type must be either TEAM or USER.")
	}

	ghesAPIURL, _ := cmd.Flags().GetString("ghes-api-url")
	targetAPIURL, _ := cmd.Flags().GetString("target-api-url")
	if ghesAPIURL != "" && targetAPIURL != "" {
		return cmdutil.NewUserError("Only one of --ghes-api-url or --target-api-url can be set at a time.")
	}

	return nil
}
