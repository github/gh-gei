package main

import (
	"context"
	"encoding/json"
	"fmt"
	"strconv"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// rewirePipelineAdoAPI defines the ADO API methods needed by rewire-pipeline.
type rewirePipelineAdoAPI interface {
	GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error)
	GetPipeline(ctx context.Context, org, teamProject string, pipelineId int) (ado.PipelineInfo, error)
}

// rewirePipelineTriggerService defines the pipeline trigger service capability.
type rewirePipelineTriggerService interface {
	RewirePipelineToGitHub(ctx context.Context, adoOrg, teamProject string, pipelineId int, defaultBranch, clean, checkoutSubmodules string, githubOrg, githubRepo, connectedServiceId string, originalTriggers json.RawMessage, targetApiUrl string) (bool, error)
}

// rewirePipelineTestService defines the pipeline test service for dry-run mode.
type rewirePipelineTestService interface {
	TestPipeline(ctx context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error)
}

// rewirePipelineEnvProvider provides environment variable fallbacks.
type rewirePipelineEnvProvider interface {
	ADOPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type rewirePipelineArgs struct {
	adoOrg                string
	adoTeamProject        string
	adoPipeline           string
	adoPipelineId         string // string to handle optional int; "" = not set
	githubOrg             string
	githubRepo            string
	serviceConnectionId   string
	adoPAT                string
	targetApiUrl          string
	dryRun                bool
	monitorTimeoutMinutes int
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newRewirePipelineCmd(
	adoAPI rewirePipelineAdoAPI,
	triggerSvc rewirePipelineTriggerService,
	testSvc rewirePipelineTestService,
	envProv rewirePipelineEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a rewirePipelineArgs

	cmd := &cobra.Command{
		Use:   "rewire-pipeline",
		Short: "Rewires an Azure DevOps pipeline to point to a GitHub repo",
		Long: "Rewires an Azure DevOps pipeline to point to a GitHub repo instead of an Azure DevOps repo.\n" +
			"Can be run in --dry-run mode to test the rewiring without making permanent changes.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runRewirePipeline(cmd.Context(), adoAPI, triggerSvc, testSvc, envProv, log, a)
		},
	}

	registerRewirePipelineFlags(cmd, &a)
	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newRewirePipelineCmdLive() *cobra.Command {
	var a rewirePipelineArgs

	cmd := &cobra.Command{
		Use:   "rewire-pipeline",
		Short: "Rewires an Azure DevOps pipeline to point to a GitHub repo",
		Long: "Rewires an Azure DevOps pipeline to point to a GitHub repo instead of an Azure DevOps repo.\n" +
			"Can be run in --dry-run mode to test the rewiring without making permanent changes.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &rewirePipelineEnvAdapter{prov: env.New()}

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			adoClient := ado.NewClient("https://dev.azure.com", adoPAT, log)
			triggerSvc := ado.NewPipelineTriggerService(adoClient, log, "https://dev.azure.com")
			testSvc := ado.NewPipelineTestService(adoClient, triggerSvc, log)

			return runRewirePipeline(cmd.Context(), adoClient, triggerSvc, testSvc, envProv, log, a)
		},
	}

	registerRewirePipelineFlags(cmd, &a)
	return cmd
}

func registerRewirePipelineFlags(cmd *cobra.Command, a *rewirePipelineArgs) {
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPipeline, "ado-pipeline", "", "Azure DevOps pipeline name")
	cmd.Flags().StringVar(&a.adoPipelineId, "ado-pipeline-id", "", "Azure DevOps pipeline ID")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.serviceConnectionId, "service-connection-id", "", "Azure DevOps service connection ID (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")
	cmd.Flags().StringVar(&a.targetApiUrl, "target-api-url", "", "Target GitHub API URL (for GHES)")
	cmd.Flags().BoolVar(&a.dryRun, "dry-run", false, "Test the pipeline rewiring without making permanent changes")
	cmd.Flags().IntVar(&a.monitorTimeoutMinutes, "monitor-timeout-minutes", 30, "Timeout in minutes for monitoring build progress during dry-run")
}

// rewirePipelineEnvAdapter wraps env.Provider to satisfy rewirePipelineEnvProvider.
type rewirePipelineEnvAdapter struct {
	prov *env.Provider
}

func (a *rewirePipelineEnvAdapter) ADOPAT() string { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateRewirePipelineArgs(a *rewirePipelineArgs) error {
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
	if err := cmdutil.ValidateRequired(a.serviceConnectionId, "--service-connection-id"); err != nil {
		return err
	}

	// Exactly one of --ado-pipeline or --ado-pipeline-id must be set
	if err := cmdutil.ValidateMutuallyExclusive(a.adoPipeline, "--ado-pipeline", a.adoPipelineId, "--ado-pipeline-id"); err != nil {
		return err
	}
	if a.adoPipeline == "" && a.adoPipelineId == "" {
		return cmdutil.NewUserError("either --ado-pipeline or --ado-pipeline-id must be specified")
	}

	// Validate pipeline ID is a valid integer if provided
	if a.adoPipelineId != "" {
		if _, err := strconv.Atoi(a.adoPipelineId); err != nil {
			return cmdutil.NewUserErrorf("--ado-pipeline-id must be a valid integer, got: %s", a.adoPipelineId)
		}
	}

	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runRewirePipeline(
	ctx context.Context,
	adoAPI rewirePipelineAdoAPI,
	triggerSvc rewirePipelineTriggerService,
	testSvc rewirePipelineTestService,
	envProv rewirePipelineEnvProvider,
	log *logger.Logger,
	a rewirePipelineArgs,
) error {
	if err := validateRewirePipelineArgs(&a); err != nil {
		return err
	}

	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	if a.dryRun {
		return handleDryRun(ctx, testSvc, log, a)
	}
	return handleRegularRewire(ctx, adoAPI, triggerSvc, log, a)
}

func handleDryRun(
	ctx context.Context,
	testSvc rewirePipelineTestService,
	log *logger.Logger,
	a rewirePipelineArgs,
) error {
	log.Info("Starting dry-run mode: Testing pipeline rewiring to GitHub...")
	log.Info("Monitor timeout: %d minutes", a.monitorTimeoutMinutes)

	testArgs := ado.PipelineTestArgs{
		AdoOrg:                a.adoOrg,
		AdoTeamProject:        a.adoTeamProject,
		PipelineName:          a.adoPipeline,
		GithubOrg:             a.githubOrg,
		GithubRepo:            a.githubRepo,
		ServiceConnectionId:   a.serviceConnectionId,
		MonitorTimeoutMinutes: a.monitorTimeoutMinutes,
		TargetApiUrl:          a.targetApiUrl,
	}

	if a.adoPipelineId != "" {
		id, _ := strconv.Atoi(a.adoPipelineId) // already validated
		testArgs.PipelineId = &id
	}

	result, err := testSvc.TestPipeline(ctx, testArgs)
	if err != nil {
		return err
	}

	log.Info("=== PIPELINE TEST REPORT ===")
	log.Info("ADO Organization: %s", result.AdoOrg)
	log.Info("ADO Team Project: %s", result.AdoTeamProject)
	log.Info("Pipeline Name: %s", result.PipelineName)

	resultStr := result.Result
	if resultStr == "" {
		resultStr = "not completed"
	}
	log.Info("Build Result: %s", resultStr)

	switch {
	case result.Result == "succeeded":
		log.Success("Pipeline test PASSED - Build completed successfully")
	case result.Result == "failed":
		log.Errorf("Pipeline test FAILED - Build completed with failures")
	case result.ErrorMessage != "":
		log.Errorf("Pipeline test FAILED - Error: %s", result.ErrorMessage)
	default:
		log.Warning("Pipeline test completed with unknown result")
	}

	return nil
}

func handleRegularRewire(
	ctx context.Context,
	adoAPI rewirePipelineAdoAPI,
	triggerSvc rewirePipelineTriggerService,
	log *logger.Logger,
	a rewirePipelineArgs,
) error {
	log.Info("Rewiring Pipeline to GitHub repo...")

	pipelineId, err := resolvePipelineId(ctx, adoAPI, log, a)
	if err != nil {
		return err
	}

	pipelineInfo, err := adoAPI.GetPipeline(ctx, a.adoOrg, a.adoTeamProject, pipelineId)
	if err != nil {
		return err
	}

	rewired, err := triggerSvc.RewirePipelineToGitHub(
		ctx, a.adoOrg, a.adoTeamProject, pipelineId,
		pipelineInfo.DefaultBranch, pipelineInfo.Clean, pipelineInfo.CheckoutSubmodules,
		a.githubOrg, a.githubRepo, a.serviceConnectionId,
		pipelineInfo.Triggers, a.targetApiUrl,
	)
	if err != nil {
		return err
	}

	if rewired {
		log.Success("Successfully rewired pipeline")
	}

	return nil
}

func resolvePipelineId(
	ctx context.Context,
	adoAPI rewirePipelineAdoAPI,
	log *logger.Logger,
	a rewirePipelineArgs,
) (int, error) {
	if a.adoPipelineId != "" {
		id, _ := strconv.Atoi(a.adoPipelineId) // already validated
		log.Info("Using provided pipeline ID: %d", id)
		return id, nil
	}

	log.Info("Looking up pipeline ID for: %s", a.adoPipeline)
	pipelineId, err := adoAPI.GetPipelineId(ctx, a.adoOrg, a.adoTeamProject, a.adoPipeline)
	if err != nil {
		return 0, fmt.Errorf("pipeline lookup failed: %w", err)
	}
	log.Info("Using resolved pipeline ID: %d", pipelineId)
	return pipelineId, nil
}
