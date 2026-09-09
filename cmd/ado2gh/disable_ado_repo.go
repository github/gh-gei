package main

import (
	"context"
	"fmt"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// disableAdoRepoAPI defines the ADO API methods needed by disable-ado-repo.
type disableAdoRepoAPI interface {
	GetRepos(ctx context.Context, org, teamProject string) ([]ado.Repository, error)
	DisableRepo(ctx context.Context, org, teamProject, repoId string) error
}

// disableAdoRepoEnvProvider provides environment variable fallbacks.
type disableAdoRepoEnvProvider interface {
	ADOPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type disableAdoRepoArgs struct {
	adoOrg         string
	adoTeamProject string
	adoRepo        string
	adoPAT         string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newDisableAdoRepoCmd(
	adoAPI disableAdoRepoAPI,
	envProv disableAdoRepoEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a disableAdoRepoArgs

	cmd := &cobra.Command{
		Use:   "disable-ado-repo",
		Short: "Disables the repo in Azure DevOps",
		Long: "Disables the repo in Azure DevOps. This makes the repo non-readable for all.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runDisableAdoRepo(cmd.Context(), adoAPI, envProv, log, a)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoRepo, "ado-repo", "", "Azure DevOps repository name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newDisableAdoRepoCmdLive() *cobra.Command {
	var a disableAdoRepoArgs

	cmd := &cobra.Command{
		Use:   "disable-ado-repo",
		Short: "Disables the repo in Azure DevOps",
		Long: "Disables the repo in Azure DevOps. This makes the repo non-readable for all.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &disableAdoRepoEnvAdapter{prov: env.New()}

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			adoAPI := ado.NewClient("https://dev.azure.com", adoPAT, log)

			return runDisableAdoRepo(cmd.Context(), adoAPI, envProv, log, a)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoRepo, "ado-repo", "", "Azure DevOps repository name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")

	return cmd
}

// disableAdoRepoEnvAdapter wraps env.Provider to satisfy disableAdoRepoEnvProvider.
type disableAdoRepoEnvAdapter struct {
	prov *env.Provider
}

func (a *disableAdoRepoEnvAdapter) ADOPAT() string { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateDisableAdoRepoArgs(a *disableAdoRepoArgs) error {
	if err := cmdutil.ValidateRequired(a.adoOrg, "--ado-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoTeamProject, "--ado-team-project"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoRepo, "--ado-repo"); err != nil {
		return err
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runDisableAdoRepo(
	ctx context.Context,
	adoAPI disableAdoRepoAPI,
	envProv disableAdoRepoEnvProvider,
	log *logger.Logger,
	a disableAdoRepoArgs,
) error {
	if err := validateDisableAdoRepoArgs(&a); err != nil {
		return err
	}

	log.Info("Disabling repo...")

	// Resolve token from flag or environment
	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	allRepos, err := adoAPI.GetRepos(ctx, a.adoOrg, a.adoTeamProject)
	if err != nil {
		return err
	}

	// Check if already disabled
	for _, r := range allRepos {
		if r.Name == a.adoRepo && r.IsDisabled {
			log.Success("Repo '%s/%s/%s' is already disabled - No action will be performed", a.adoOrg, a.adoTeamProject, a.adoRepo)
			return nil
		}
	}

	// Find the repo ID
	var repoId string
	for _, r := range allRepos {
		if r.Name == a.adoRepo {
			repoId = r.ID
			break
		}
	}

	if repoId == "" {
		return fmt.Errorf("repo %q not found in %s/%s", a.adoRepo, a.adoOrg, a.adoTeamProject)
	}

	if err := adoAPI.DisableRepo(ctx, a.adoOrg, a.adoTeamProject, repoId); err != nil {
		return err
	}

	log.Success("Repo successfully disabled")

	return nil
}
