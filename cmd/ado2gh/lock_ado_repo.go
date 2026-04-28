package main

import (
	"context"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// lockAdoRepoAPI defines the ADO API methods needed by lock-ado-repo.
type lockAdoRepoAPI interface {
	GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error)
	GetRepoId(ctx context.Context, org, teamProject, repo string) (string, error)
	GetIdentityDescriptor(ctx context.Context, org, teamProjectId, groupName string) (string, error)
	LockRepo(ctx context.Context, org, teamProjectId, repoId, identityDescriptor string) error
}

// lockAdoRepoEnvProvider provides environment variable fallbacks.
type lockAdoRepoEnvProvider interface {
	ADOPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type lockAdoRepoArgs struct {
	adoOrg         string
	adoTeamProject string
	adoRepo        string
	adoPAT         string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newLockAdoRepoCmd(
	adoAPI lockAdoRepoAPI,
	envProv lockAdoRepoEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a lockAdoRepoArgs

	cmd := &cobra.Command{
		Use:   "lock-ado-repo",
		Short: "Makes the ADO repo read-only for all users",
		Long: "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runLockAdoRepo(cmd.Context(), adoAPI, envProv, log, a)
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

func newLockAdoRepoCmdLive() *cobra.Command {
	var a lockAdoRepoArgs

	cmd := &cobra.Command{
		Use:   "lock-ado-repo",
		Short: "Makes the ADO repo read-only for all users",
		Long: "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &lockAdoRepoEnvAdapter{prov: env.New()}

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			adoAPI := ado.NewClient("https://dev.azure.com", adoPAT, log)

			return runLockAdoRepo(cmd.Context(), adoAPI, envProv, log, a)
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

// lockAdoRepoEnvAdapter wraps env.Provider to satisfy lockAdoRepoEnvProvider.
type lockAdoRepoEnvAdapter struct {
	prov *env.Provider
}

func (a *lockAdoRepoEnvAdapter) ADOPAT() string { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateLockAdoRepoArgs(a *lockAdoRepoArgs) error {
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

func runLockAdoRepo(
	ctx context.Context,
	adoAPI lockAdoRepoAPI,
	envProv lockAdoRepoEnvProvider,
	log *logger.Logger,
	a lockAdoRepoArgs,
) error {
	if err := validateLockAdoRepoArgs(&a); err != nil {
		return err
	}

	log.Info("Locking repo...")

	// Resolve token from flag or environment
	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	teamProjectId, err := adoAPI.GetTeamProjectId(ctx, a.adoOrg, a.adoTeamProject)
	if err != nil {
		return err
	}

	repoId, err := adoAPI.GetRepoId(ctx, a.adoOrg, a.adoTeamProject, a.adoRepo)
	if err != nil {
		return err
	}

	identityDescriptor, err := adoAPI.GetIdentityDescriptor(ctx, a.adoOrg, teamProjectId, "Project Valid Users")
	if err != nil {
		return err
	}

	if err := adoAPI.LockRepo(ctx, a.adoOrg, teamProjectId, repoId, identityDescriptor); err != nil {
		return err
	}

	log.Success("Repo successfully locked")

	return nil
}
