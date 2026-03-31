package sharedcmd

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
)

// TeamCreator is the consumer-defined interface for create-team.
type TeamCreator interface {
	GetTeams(ctx context.Context, org string) ([]github.Team, error)
	CreateTeam(ctx context.Context, org, name string) (*github.Team, error)
	GetTeamMembers(ctx context.Context, org, teamSlug string) ([]string, error)
	RemoveTeamMember(ctx context.Context, org, teamSlug, member string) error
	GetIdpGroupId(ctx context.Context, org, groupName string) (int, error)
	AddEmuGroupToTeam(ctx context.Context, org, teamSlug string, groupID int) error
}

func ValidateCreateTeamArgs(githubOrg, teamName string) error {
	if strings.TrimSpace(githubOrg) == "" {
		return cmdutil.NewUserError("--github-org must be provided")
	}
	if strings.HasPrefix(githubOrg, "http://") || strings.HasPrefix(githubOrg, "https://") {
		return cmdutil.NewUserError("The --github-org option expects an organization name, not a URL. Please provide just the organization name.")
	}
	if strings.TrimSpace(teamName) == "" {
		return cmdutil.NewUserError("--team-name must be provided")
	}
	return nil
}

func RunCreateTeam(ctx context.Context, gh TeamCreator, log *logger.Logger, githubOrg, teamName, idpGroup string) error {
	log.Info("Creating GitHub team...")

	teams, err := gh.GetTeams(ctx, githubOrg)
	if err != nil {
		return err
	}

	var teamSlug string
	for _, t := range teams {
		if t.Name == teamName {
			teamSlug = t.Slug
			break
		}
	}

	if teamSlug != "" {
		log.Success("Team '%s' already exists. New team will not be created", teamName)
	} else {
		team, err := gh.CreateTeam(ctx, githubOrg, teamName)
		if err != nil {
			return err
		}
		teamSlug = team.Slug
		log.Success("Successfully created team")
	}

	if strings.TrimSpace(idpGroup) == "" {
		log.Info("No IdP Group provided, skipping the IdP linking step")
	} else {
		members, err := gh.GetTeamMembers(ctx, githubOrg, teamSlug)
		if err != nil {
			return err
		}
		for _, member := range members {
			if err := gh.RemoveTeamMember(ctx, githubOrg, teamSlug, member); err != nil {
				return err
			}
		}
		idpGroupID, err := gh.GetIdpGroupId(ctx, githubOrg, idpGroup)
		if err != nil {
			return err
		}
		if err := gh.AddEmuGroupToTeam(ctx, githubOrg, teamSlug, idpGroupID); err != nil {
			return err
		}
		log.Success("Successfully linked team to Idp group")
	}

	return nil
}
