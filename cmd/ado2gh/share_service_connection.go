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

// shareServiceConnectionAdoAPI defines the ADO API methods needed by share-service-connection.
type shareServiceConnectionAdoAPI interface {
	GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error)
	ContainsServiceConnection(ctx context.Context, org, teamProject, serviceConnectionId string) (bool, error)
	ShareServiceConnection(ctx context.Context, org, teamProject, teamProjectId, serviceConnectionId string) error
}

// shareServiceConnectionEnvProvider provides environment variable fallbacks.
type shareServiceConnectionEnvProvider interface {
	ADOPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type shareServiceConnectionArgs struct {
	adoOrg              string
	adoTeamProject      string
	serviceConnectionID string
	adoPAT              string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newShareServiceConnectionCmd(
	adoAPI shareServiceConnectionAdoAPI,
	envProv shareServiceConnectionEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a shareServiceConnectionArgs

	cmd := &cobra.Command{
		Use:   "share-service-connection",
		Short: "Shares a service connection with a team project",
		Long: "Makes an existing GitHub Pipelines App service connection available in another team project. This is required before you can rewire pipelines.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runShareServiceConnection(cmd.Context(), adoAPI, envProv, log, a)
		},
	}

	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.serviceConnectionID, "service-connection-id", "", "Service connection ID to share (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newShareServiceConnectionCmdLive() *cobra.Command {
	var a shareServiceConnectionArgs

	cmd := &cobra.Command{
		Use:   "share-service-connection",
		Short: "Shares a service connection with a team project",
		Long: "Makes an existing GitHub Pipelines App service connection available in another team project. This is required before you can rewire pipelines.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &shareServiceConnectionEnvAdapter{prov: env.New()}

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			adoClient := ado.NewClient("https://dev.azure.com", adoPAT, log)

			return runShareServiceConnection(cmd.Context(), adoClient, envProv, log, a)
		},
	}

	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.serviceConnectionID, "service-connection-id", "", "Service connection ID to share (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")

	return cmd
}

type shareServiceConnectionEnvAdapter struct {
	prov *env.Provider
}

func (a *shareServiceConnectionEnvAdapter) ADOPAT() string { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateShareServiceConnectionArgs(a *shareServiceConnectionArgs) error {
	if err := cmdutil.ValidateRequired(a.adoOrg, "--ado-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.adoTeamProject, "--ado-team-project"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.serviceConnectionID, "--service-connection-id"); err != nil {
		return err
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runShareServiceConnection(
	ctx context.Context,
	adoAPI shareServiceConnectionAdoAPI,
	envProv shareServiceConnectionEnvProvider,
	log *logger.Logger,
	a shareServiceConnectionArgs,
) error {
	if err := validateShareServiceConnectionArgs(&a); err != nil {
		return err
	}

	log.Info("Sharing Service Connection...")

	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	teamProjectID, err := adoAPI.GetTeamProjectId(ctx, a.adoOrg, a.adoTeamProject)
	if err != nil {
		return err
	}

	alreadyShared, err := adoAPI.ContainsServiceConnection(ctx, a.adoOrg, a.adoTeamProject, a.serviceConnectionID)
	if err != nil {
		return err
	}

	if alreadyShared {
		log.Info("Service connection already shared with team project")
		return nil
	}

	if err := adoAPI.ShareServiceConnection(ctx, a.adoOrg, a.adoTeamProject, teamProjectID, a.serviceConnectionID); err != nil {
		return err
	}

	log.Success("Successfully shared service connection")
	return nil
}
