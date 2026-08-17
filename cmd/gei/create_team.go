package main

import (
	"context"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// teamCreator is the consumer-defined interface for create-team.
type teamCreator = sharedcmd.TeamCreator

// newCreateTeamCmd creates the create-team cobra command.
func newCreateTeamCmd(gh teamCreator, log *logger.Logger) *cobra.Command {
	var (
		githubOrg string
		teamName  string
		idpGroup  string
	)

	cmd := &cobra.Command{
		Use:   "create-team",
		Short: "Creates a GitHub team and optionally links it to an IdP group",
		Long:  "Creates a GitHub team and optionally links it to an IdP group.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := sharedcmd.ValidateCreateTeamArgs(githubOrg, teamName); err != nil {
				return err
			}
			return sharedcmd.RunCreateTeam(cmd.Context(), gh, log, githubOrg, teamName, idpGroup)
		},
	}

	cmd.Flags().StringVar(&githubOrg, "github-org", "", "The GitHub organization to create the team in (REQUIRED)")
	cmd.Flags().StringVar(&teamName, "team-name", "", "The name of the team to create (REQUIRED)")
	cmd.Flags().StringVar(&idpGroup, "idp-group", "", "The name of the IdP group to link to the team")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// validateCreateTeamArgs delegates to sharedcmd for backward compat with tests.
func validateCreateTeamArgs(githubOrg, teamName string) error {
	return sharedcmd.ValidateCreateTeamArgs(githubOrg, teamName)
}

// runCreateTeam delegates to sharedcmd for backward compat with tests.
func runCreateTeam(ctx context.Context, gh teamCreator, log *logger.Logger, githubOrg, teamName, idpGroup string) error {
	return sharedcmd.RunCreateTeam(ctx, gh, log, githubOrg, teamName, idpGroup)
}
