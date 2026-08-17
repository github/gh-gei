package main

import (
	"context"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// addTeamToRepoGitHub defines the GitHub API methods needed by add-team-to-repo.
type addTeamToRepoGitHub interface {
	GetTeamSlug(ctx context.Context, org, teamName string) (string, error)
	AddTeamToRepo(ctx context.Context, org, teamSlug, repo, role string) error
}

// addTeamToRepoEnvProvider provides environment variable fallbacks.
type addTeamToRepoEnvProvider interface {
	TargetGitHubPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type addTeamToRepoArgs struct {
	githubOrg    string
	githubRepo   string
	team         string
	role         string
	githubPAT    string
	targetAPIURL string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newAddTeamToRepoCmd(
	gh addTeamToRepoGitHub,
	envProv addTeamToRepoEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a addTeamToRepoArgs

	cmd := &cobra.Command{
		Use:   "add-team-to-repo",
		Short: "Adds a team to a repo with a specific role/permission",
		Long: "Adds a team to a repo with a specific role/permission\n" +
			"Note: Expects GH_PAT env variable or --github-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runAddTeamToRepo(cmd.Context(), gh, envProv, log, a)
		},
	}

	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.team, "team", "", "Team name (REQUIRED)")
	cmd.Flags().StringVar(&a.role, "role", "", "Role/permission: pull, push, admin, maintain, triage (REQUIRED)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newAddTeamToRepoCmdLive() *cobra.Command {
	var a addTeamToRepoArgs

	cmd := &cobra.Command{
		Use:   "add-team-to-repo",
		Short: "Adds a team to a repo with a specific role/permission",
		Long: "Adds a team to a repo with a specific role/permission\n" +
			"Note: Expects GH_PAT env variable or --github-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &addTeamToRepoEnvAdapter{prov: env.New()}

			githubPAT := a.githubPAT
			if githubPAT == "" {
				githubPAT = envProv.TargetGitHubPAT()
			}

			apiURL := a.targetAPIURL
			if apiURL == "" {
				apiURL = "https://api.github.com"
			}

			gh := github.NewClient(githubPAT,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			return runAddTeamToRepo(cmd.Context(), gh, envProv, log, a)
		},
	}

	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.team, "team", "", "Team name (REQUIRED)")
	cmd.Flags().StringVar(&a.role, "role", "", "Role/permission: pull, push, admin, maintain, triage (REQUIRED)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

type addTeamToRepoEnvAdapter struct {
	prov *env.Provider
}

func (a *addTeamToRepoEnvAdapter) TargetGitHubPAT() string { return a.prov.TargetGitHubPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateAddTeamToRepoArgs(a *addTeamToRepoArgs) error {
	if err := cmdutil.ValidateRequired(a.githubOrg, "--github-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubRepo, "--github-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.team, "--team"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.role, "--role"); err != nil {
		return err
	}
	if err := cmdutil.ValidateOneOf(a.role, "--role", "pull", "push", "admin", "maintain", "triage"); err != nil {
		return err
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runAddTeamToRepo(
	ctx context.Context,
	gh addTeamToRepoGitHub,
	envProv addTeamToRepoEnvProvider,
	log *logger.Logger,
	a addTeamToRepoArgs,
) error {
	if err := validateAddTeamToRepoArgs(&a); err != nil {
		return err
	}

	log.Info("Adding team to repo...")

	teamSlug, err := gh.GetTeamSlug(ctx, a.githubOrg, a.team)
	if err != nil {
		return err
	}

	if err := gh.AddTeamToRepo(ctx, a.githubOrg, teamSlug, a.githubRepo, a.role); err != nil {
		return err
	}

	log.Success("Successfully added team to repo")
	return nil
}
