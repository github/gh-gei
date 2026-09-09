package main

import (
	"context"
	"fmt"
	"net/url"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// configureAutolinkGitHub defines the GitHub API methods needed by configure-autolink.
type configureAutolinkGitHub interface {
	GetAutoLinks(ctx context.Context, org, repo string) ([]github.AutoLink, error)
	AddAutoLink(ctx context.Context, org, repo, keyPrefix, urlTemplate string) error
	DeleteAutoLink(ctx context.Context, org, repo string, autoLinkID int) error
}

// configureAutolinkEnvProvider provides environment variable fallbacks.
type configureAutolinkEnvProvider interface {
	TargetGitHubPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type configureAutolinkArgs struct {
	githubOrg      string
	githubRepo     string
	adoOrg         string
	adoTeamProject string
	githubPAT      string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newConfigureAutolinkCmd(
	gh configureAutolinkGitHub,
	envProv configureAutolinkEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a configureAutolinkArgs

	cmd := &cobra.Command{
		Use:   "configure-autolink",
		Short: "Configures Autolink References in GitHub for Azure Boards work items",
		Long: "Configures Autolink References in GitHub so that references to Azure Boards work items become hyperlinks in GitHub\n" +
			"Note: Expects GH_PAT env variable or --github-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runConfigureAutolink(cmd.Context(), gh, envProv, log, a)
		},
	}

	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newConfigureAutolinkCmdLive() *cobra.Command {
	var a configureAutolinkArgs

	cmd := &cobra.Command{
		Use:   "configure-autolink",
		Short: "Configures Autolink References in GitHub for Azure Boards work items",
		Long: "Configures Autolink References in GitHub so that references to Azure Boards work items become hyperlinks in GitHub\n" +
			"Note: Expects GH_PAT env variable or --github-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &configureAutolinkEnvAdapter{prov: env.New()}

			githubPAT := a.githubPAT
			if githubPAT == "" {
				githubPAT = envProv.TargetGitHubPAT()
			}

			gh := github.NewClient(githubPAT,
				github.WithLogger(log),
				github.WithVersion(version),
			)

			return runConfigureAutolink(cmd.Context(), gh, envProv, log, a)
		},
	}

	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "GitHub personal access token (falls back to GH_PAT env)")

	return cmd
}

type configureAutolinkEnvAdapter struct {
	prov *env.Provider
}

func (a *configureAutolinkEnvAdapter) TargetGitHubPAT() string { return a.prov.TargetGitHubPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateConfigureAutolinkArgs(a *configureAutolinkArgs) error {
	if err := cmdutil.ValidateRequired(a.githubOrg, "--github-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubRepo, "--github-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoOrg, "--ado-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoTeamProject, "--ado-team-project"); err != nil {
		return err
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runConfigureAutolink(
	ctx context.Context,
	gh configureAutolinkGitHub,
	_ configureAutolinkEnvProvider,
	log *logger.Logger,
	a configureAutolinkArgs,
) error {
	if err := validateConfigureAutolinkArgs(&a); err != nil {
		return err
	}

	log.Info("Configuring Autolink Reference...")

	keyPrefix := "AB#"
	urlTemplate := fmt.Sprintf("https://dev.azure.com/%s/%s/_workitems/edit/<num>/",
		url.PathEscape(a.adoOrg),
		url.PathEscape(a.adoTeamProject),
	)

	autoLinks, err := gh.GetAutoLinks(ctx, a.githubOrg, a.githubRepo)
	if err != nil {
		return err
	}

	// Check if an autolink with matching prefix AND template already exists
	for _, al := range autoLinks {
		if al.KeyPrefix == keyPrefix && al.URLTemplate == urlTemplate {
			log.Success("Autolink reference already exists for key_prefix: 'AB#'. No operation will be performed")
			return nil
		}
	}

	// Check if an autolink with matching prefix but wrong template exists
	for _, al := range autoLinks {
		if al.KeyPrefix == keyPrefix {
			log.Info("Autolink reference already exists for key_prefix: 'AB#', but the url template is incorrect")
			log.Info("Deleting existing Autolink reference for key_prefix: 'AB#' before creating a new Autolink reference")
			if err := gh.DeleteAutoLink(ctx, a.githubOrg, a.githubRepo, al.ID); err != nil {
				return err
			}
			break
		}
	}

	if err := gh.AddAutoLink(ctx, a.githubOrg, a.githubRepo, keyPrefix, urlTemplate); err != nil {
		return err
	}

	log.Success("Successfully configured autolink references")
	return nil
}
