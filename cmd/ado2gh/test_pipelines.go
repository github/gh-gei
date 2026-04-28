package main

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"regexp"
	"strings"
	"sync"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// testPipelinesAdoAPI defines the ADO API methods needed by test-pipelines.
type testPipelinesAdoAPI interface {
	GetEnabledRepos(ctx context.Context, org, teamProject string) ([]ado.Repository, error)
	GetPipelines(ctx context.Context, org, teamProject, repoId string) ([]string, error)
	GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error)
}

// testPipelinesTestService defines the pipeline test service for batch testing.
type testPipelinesTestService interface {
	TestPipeline(ctx context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error)
}

// testPipelinesEnvProvider provides environment variable fallbacks.
type testPipelinesEnvProvider interface {
	ADOPAT() string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type testPipelinesArgs struct {
	adoOrg                string
	adoTeamProject        string
	githubOrg             string
	githubRepo            string
	serviceConnectionId   string
	adoPAT                string
	targetApiUrl          string
	monitorTimeoutMinutes int
	pipelineFilter        string
	maxConcurrentTests    int
	reportPath            string
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newTestPipelinesCmd(
	adoAPI testPipelinesAdoAPI,
	testSvc testPipelinesTestService,
	envProv testPipelinesEnvProvider,
	log *logger.Logger,
) *cobra.Command {
	var a testPipelinesArgs

	cmd := &cobra.Command{
		Use:   "test-pipelines",
		Short: "Batch test Azure DevOps pipelines by rewiring them to a GitHub repo",
		Long: "Discovers pipelines in enabled repos, temporarily rewires each to GitHub,\n" +
			"runs a build, restores the original configuration, and generates a report.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runTestPipelines(cmd.Context(), adoAPI, testSvc, envProv, log, a)
		},
	}

	registerTestPipelinesFlags(cmd, &a)
	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newTestPipelinesCmdLive() *cobra.Command {
	var a testPipelinesArgs

	cmd := &cobra.Command{
		Use:   "test-pipelines",
		Short: "Batch test Azure DevOps pipelines by rewiring them to a GitHub repo",
		Long: "Discovers pipelines in enabled repos, temporarily rewires each to GitHub,\n" +
			"runs a build, restores the original configuration, and generates a report.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := &testPipelinesEnvAdapter{prov: env.New()}

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			adoClient := ado.NewClient("https://dev.azure.com", adoPAT, log)
			triggerSvc := ado.NewPipelineTriggerService(adoClient, log, "https://dev.azure.com")
			testSvc := ado.NewPipelineTestService(adoClient, triggerSvc, log)

			return runTestPipelines(cmd.Context(), adoClient, testSvc, envProv, log, a)
		},
	}

	registerTestPipelinesFlags(cmd, &a)
	return cmd
}

func registerTestPipelinesFlags(cmd *cobra.Command, a *testPipelinesArgs) {
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "GitHub organization name (REQUIRED)")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "GitHub repository name (REQUIRED)")
	cmd.Flags().StringVar(&a.serviceConnectionId, "service-connection-id", "", "Azure DevOps service connection ID (REQUIRED)")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "Azure DevOps personal access token (falls back to ADO_PAT env)")
	cmd.Flags().StringVar(&a.targetApiUrl, "target-api-url", "", "Target GitHub API URL (for GHES)")
	cmd.Flags().IntVar(&a.monitorTimeoutMinutes, "monitor-timeout-minutes", 30, "Timeout in minutes for monitoring build progress")
	cmd.Flags().StringVar(&a.pipelineFilter, "pipeline-filter", "", "Wildcard filter for pipeline names (* and ? supported)")
	cmd.Flags().IntVar(&a.maxConcurrentTests, "max-concurrent-tests", 3, "Maximum number of concurrent pipeline tests")
	cmd.Flags().StringVar(&a.reportPath, "report-path", "pipeline-test-report.json", "Path for the JSON test report")
}

// testPipelinesEnvAdapter wraps env.Provider to satisfy testPipelinesEnvProvider.
type testPipelinesEnvAdapter struct {
	prov *env.Provider
}

func (a *testPipelinesEnvAdapter) ADOPAT() string { return a.prov.ADOPAT() }

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateTestPipelinesArgs(a *testPipelinesArgs) error {
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
	if a.maxConcurrentTests < 1 {
		return cmdutil.NewUserError("--max-concurrent-tests must be at least 1")
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runTestPipelines(
	ctx context.Context,
	adoAPI testPipelinesAdoAPI,
	testSvc testPipelinesTestService,
	envProv testPipelinesEnvProvider,
	log *logger.Logger,
	a testPipelinesArgs,
) error {
	if err := validateTestPipelinesArgs(&a); err != nil {
		return err
	}

	if a.adoPAT == "" {
		a.adoPAT = envProv.ADOPAT()
	}

	log.Info("Starting batch pipeline testing...")

	summary := &ado.PipelineTestSummary{}
	startTime := time.Now()

	// Step 1: Discover pipelines
	log.Info("Step 1: Discovering pipelines...")
	pipelines, err := discoverPipelines(ctx, adoAPI, log, a)
	if err != nil {
		return err
	}
	summary.TotalPipelines = len(pipelines)
	log.Info("Found %d pipelines to test", len(pipelines))

	if len(pipelines) == 0 {
		log.Warning("No pipelines found matching the criteria")
		return nil
	}

	// Step 2: Test pipelines with concurrency control
	log.Info("Step 2: Testing pipelines (max concurrent: %d)...", a.maxConcurrentTests)
	results := testPipelinesWithConcurrency(ctx, testSvc, log, a, pipelines)
	summary.AddResults(results)

	// Step 3: Compute summary statistics
	summary.TotalTestTime = time.Since(startTime)
	for _, r := range results {
		switch {
		case r.IsSuccessful():
			summary.SuccessfulBuilds++
		case r.IsFailed():
			summary.FailedBuilds++
		case !r.IsCompleted() && r.Status == "timedOut":
			summary.TimedOutBuilds++
		}
		if !r.RewiredSuccessfully {
			summary.ErrorsRewiring++
		}
		if !r.RestoredSuccessfully {
			summary.ErrorsRestoring++
		}
	}

	// Step 4: Reports
	generateConsoleSummary(log, summary)
	if err := saveDetailedReport(summary, a.reportPath); err != nil {
		log.Warning("Failed to save report: %v", err)
	} else {
		log.Info("Detailed report saved to: %s", a.reportPath)
	}

	log.Info("Batch testing completed. Results saved to: %s", a.reportPath)
	return nil
}

// ---------------------------------------------------------------------------
// Pipeline discovery
// ---------------------------------------------------------------------------

type pipelineRef struct {
	name string
	id   int
}

func discoverPipelines(
	ctx context.Context,
	adoAPI testPipelinesAdoAPI,
	log *logger.Logger,
	a testPipelinesArgs,
) ([]pipelineRef, error) {
	repos, err := adoAPI.GetEnabledRepos(ctx, a.adoOrg, a.adoTeamProject)
	if err != nil {
		return nil, err
	}

	var pipelines []pipelineRef

	for _, repo := range repos {
		repoPipelines, err := adoAPI.GetPipelines(ctx, a.adoOrg, a.adoTeamProject, repo.ID)
		if err != nil {
			log.Warning("Could not get pipelines for repository '%s': %v", repo.Name, err)
			continue
		}

		for _, pipelineName := range repoPipelines {
			if a.pipelineFilter != "" && !matchWildcard(pipelineName, a.pipelineFilter) {
				continue
			}

			pipelineId, err := adoAPI.GetPipelineId(ctx, a.adoOrg, a.adoTeamProject, pipelineName)
			if err != nil {
				log.Warning("Could not get ID for pipeline '%s': %v", pipelineName, err)
				continue
			}

			pipelines = append(pipelines, pipelineRef{name: pipelineName, id: pipelineId})
		}
	}

	return pipelines, nil
}

// matchWildcard performs simple wildcard matching (case-insensitive).
// Supports * (any sequence) and ? (single character).
func matchWildcard(text, pattern string) bool {
	if pattern == "" || pattern == "*" {
		return true
	}

	// Convert wildcard pattern to regex
	regexStr := "^" + regexp.QuoteMeta(pattern) + "$"
	regexStr = strings.ReplaceAll(regexStr, `\*`, ".*")
	regexStr = strings.ReplaceAll(regexStr, `\?`, ".")

	re, err := regexp.Compile("(?i)" + regexStr)
	if err != nil {
		return false
	}
	return re.MatchString(text)
}

// ---------------------------------------------------------------------------
// Concurrent testing
// ---------------------------------------------------------------------------

func testPipelinesWithConcurrency(
	ctx context.Context,
	testSvc testPipelinesTestService,
	log *logger.Logger,
	a testPipelinesArgs,
	pipelines []pipelineRef,
) []ado.PipelineTestResult {
	results := make([]ado.PipelineTestResult, len(pipelines))
	sem := make(chan struct{}, a.maxConcurrentTests)
	var wg sync.WaitGroup

	for i, p := range pipelines {
		wg.Add(1)
		go func(idx int, pipeline pipelineRef) {
			defer wg.Done()

			sem <- struct{}{}        // acquire
			defer func() { <-sem }() // release

			log.Info("Testing pipeline: %s (ID: %d)", pipeline.name, pipeline.id)

			testArgs := ado.PipelineTestArgs{
				AdoOrg:                a.adoOrg,
				AdoTeamProject:        a.adoTeamProject,
				PipelineName:          pipeline.name,
				PipelineId:            &pipeline.id,
				GithubOrg:             a.githubOrg,
				GithubRepo:            a.githubRepo,
				ServiceConnectionId:   a.serviceConnectionId,
				TargetApiUrl:          a.targetApiUrl,
				MonitorTimeoutMinutes: a.monitorTimeoutMinutes,
			}

			result, err := testSvc.TestPipeline(ctx, testArgs)
			if err != nil {
				log.Warning("Pipeline '%s' test returned error: %v", pipeline.name, err)
			}
			results[idx] = result
		}(i, p)
	}

	wg.Wait()
	return results
}

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

func generateConsoleSummary(log *logger.Logger, summary *ado.PipelineTestSummary) {
	log.Info("")
	log.Info("=== PIPELINE BATCH TEST SUMMARY ===")
	log.Info("Total Pipelines Tested: %d", summary.TotalPipelines)
	log.Info("Successful Builds: %d", summary.SuccessfulBuilds)
	log.Info("Failed Builds: %d", summary.FailedBuilds)
	log.Info("Timed Out Builds: %d", summary.TimedOutBuilds)
	log.Info("Rewiring Errors: %d", summary.ErrorsRewiring)
	log.Info("Restoration Errors: %d", summary.ErrorsRestoring)
	log.Info("Success Rate: %.1f%%", summary.SuccessRate())

	hours := int(summary.TotalTestTime.Hours())
	minutes := int(summary.TotalTestTime.Minutes()) % 60
	seconds := int(summary.TotalTestTime.Seconds()) % 60
	log.Info("Total Test Time: %02d:%02d:%02d", hours, minutes, seconds)

	if summary.ErrorsRestoring > 0 {
		log.Warning("")
		log.Warning("PIPELINES REQUIRING MANUAL RESTORATION:")
		for _, r := range summary.Results {
			if !r.RestoredSuccessfully {
				log.Warning("  - %s (ID: %d) in %s/%s", r.PipelineName, r.PipelineId, r.AdoOrg, r.AdoTeamProject)
			}
		}
	}

	log.Info("=== END OF SUMMARY ===")
	log.Info("")
}

func saveDetailedReport(summary *ado.PipelineTestSummary, reportPath string) error {
	data, err := json.MarshalIndent(summary, "", "  ")
	if err != nil {
		return fmt.Errorf("marshal report: %w", err)
	}
	return os.WriteFile(reportPath, data, 0o600)
}
