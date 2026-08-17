package main

import (
	"context"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/google/uuid"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// integrateBoardsAdoAPI defines the ADO API methods needed by integrate-boards.
type integrateBoardsAdoAPI interface {
	GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error)
	GetGithubHandle(ctx context.Context, org, teamProject, githubToken string) (string, error)
	GetBoardsGithubConnection(ctx context.Context, org, teamProject string) (ado.BoardsConnection, error)
	CreateBoardsGithubEndpoint(ctx context.Context, org, teamProjectId, githubToken, githubHandle, endpointName string) (string, error)
	GetBoardsGithubRepoId(ctx context.Context, org, teamProject, teamProjectId, endpointId, githubOrg, githubRepo string) (string, error)
	CreateBoardsGithubConnection(ctx context.Context, org, teamProject, endpointId, repoId string) error
	AddRepoToBoardsGithubConnection(ctx context.Context, org, teamProject, connectionId, connectionName, endpointId string, repoIds []string) error
}

// integrateBoardsEnvProvider provides environment variable fallbacks.
type integrateBoardsEnvProvider interface {
	ADOPAT() string
	TargetGitHubPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type integrateBoardsArgs struct {
	adoOrg         string
	adoTeamProject string
	githubOrg      string
	githubRepo     string
	adoPAT         string
	githubPAT      string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newIntegrateBoardsCmd(
	adoAPI integrateBoardsAdoAPI,
	envProv integrateBoardsEnvProvider,
	log *logger.Logger,
	uuidFunc func() string,
) *cobra.Command {
	var a integrateBoardsArgs

	cmd := &cobra.Command{
		Use:   "integrate-boards",
		Short: "Configures Azure Boards and GitHub integration",
		Long: "Configures the Azure Boards<->GitHub integration in Azure DevOps.\n" +
			"Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.\n" +
			"The ADO_PAT token must have 'All organizations' access selected.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runIntegrateBoards(cmd.Context(), adoAPI, envProv, log, uuidFunc, a)
		},
	}

	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "Target GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newIntegrateBoardsCmdLive() *cobra.Command {
	var a integrateBoardsArgs

	cmd := &cobra.Command{
		Use:   "integrate-boards",
		Short: "Configures Azure Boards and GitHub integration",
		Long: "Configures the Azure Boards<->GitHub integration in Azure DevOps.\n" +
			"Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.\n" +
			"The ADO_PAT token must have 'All organizations' access selected.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &integrateBoardsEnvAdapter{prov: env.New()}

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			adoClient := ado.NewClient("https://dev.azure.com", adoPAT, log)

			uuidFunc := func() string { return uuid.New().String() }

			return runIntegrateBoards(cmd.Context(), adoClient, envProv, log, uuidFunc, a)
		},
	}

	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "Target GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")

	return cmd
}

type integrateBoardsEnvAdapter struct {
	prov *env.Provider
}

func (a *integrateBoardsEnvAdapter) TargetGitHubPAT() string { return a.prov.TargetGitHubPAT() }
func (a *integrateBoardsEnvAdapter) ADOPAT() string          { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateIntegrateBoardsArgs(a *integrateBoardsArgs) error {
	if err := cmdutil.ValidateRequired(a.adoOrg, "--ado-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoTeamProject, "--ado-team-project"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubOrg, "--github-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubRepo, "--github-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.githubOrg, "--github-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.githubRepo, "--github-repo"); err != nil {
		return err
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runIntegrateBoards(
	ctx context.Context,
	adoAPI integrateBoardsAdoAPI,
	envProv integrateBoardsEnvProvider,
	log *logger.Logger,
	uuidFunc func() string,
	a integrateBoardsArgs,
) error {
	if err := validateIntegrateBoardsArgs(&a); err != nil {
		return err
	}

	log.Info("Integrating Azure Boards...")

	if a.githubPAT == "" {
		a.githubPAT = envProv.TargetGitHubPAT()
	}
	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	teamProjectID, err := adoAPI.GetTeamProjectId(ctx, a.adoOrg, a.adoTeamProject)
	if err != nil {
		return err
	}

	githubHandle, err := adoAPI.GetGithubHandle(ctx, a.adoOrg, a.adoTeamProject, a.githubPAT)
	if err != nil {
		return err
	}

	boardsConnection, err := adoAPI.GetBoardsGithubConnection(ctx, a.adoOrg, a.adoTeamProject)
	if err != nil {
		return err
	}

	// No existing connection — create everything from scratch
	if boardsConnection.ConnectionID == "" {
		endpointID, err := adoAPI.CreateBoardsGithubEndpoint(ctx, a.adoOrg, teamProjectID, a.githubPAT, githubHandle, uuidFunc())
		if err != nil {
			return err
		}

		repoID, err := adoAPI.GetBoardsGithubRepoId(ctx, a.adoOrg, a.adoTeamProject, teamProjectID, endpointID, a.githubOrg, a.githubRepo)
		if err != nil {
			return err
		}

		if err := adoAPI.CreateBoardsGithubConnection(ctx, a.adoOrg, a.adoTeamProject, endpointID, repoID); err != nil {
			return err
		}

		log.Success("Successfully configured Boards<->GitHub integration")
		return nil
	}

	// Existing connection — add repo to it
	repoID, err := adoAPI.GetBoardsGithubRepoId(ctx, a.adoOrg, a.adoTeamProject, teamProjectID, boardsConnection.EndpointID, a.githubOrg, a.githubRepo)
	if err != nil {
		return err
	}

	// Check if repo is already integrated
	for _, existingID := range boardsConnection.RepoIDs {
		if existingID == repoID {
			log.Warning("This repo is already configured in the Boards integration (Repo ID: %s)", repoID)
			return nil
		}
	}

	repos := append(boardsConnection.RepoIDs, repoID)
	if err := adoAPI.AddRepoToBoardsGithubConnection(ctx, a.adoOrg, a.adoTeamProject, boardsConnection.ConnectionID, boardsConnection.ConnectionName, boardsConnection.EndpointID, repos); err != nil {
		return err
	}

	log.Success("Successfully configured Boards<->GitHub integration")
	return nil
}
