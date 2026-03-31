package main

import (
	"context"
	"fmt"
	"os"
	"strings"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/scriptgen"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// generateScriptAdoAPI defines the ADO API methods used directly (not via inspector).
type generateScriptAdoAPI interface {
	GetTeamProjects(ctx context.Context, org string) ([]string, error)
	GetGithubAppId(ctx context.Context, org, githubOrg string, teamProjects []string) (string, error)
}

// generateScriptInspector defines the inspector methods used by generate-script.
type generateScriptInspector interface {
	GetOrgs(ctx context.Context) ([]string, error)
	GetTeamProjects(ctx context.Context, org string) ([]string, error)
	GetRepos(ctx context.Context, org, teamProject string) ([]ado.Repository, error)
	GetPipelines(ctx context.Context, org, teamProject, repo string) ([]string, error)
	GetRepoCount(ctx context.Context) (int, error)
	LoadReposCsv(csvPath string) error
	OutputRepoListToLog()
}

const verboseFlag = " --verbose"

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type generateScriptArgs struct {
	githubOrg             string
	adoOrg                string
	adoTeamProject        string
	output                string
	sequential            bool
	adoServerURL          string
	targetAPIURL          string
	createTeams           bool
	linkIdpGroups         bool
	lockAdoRepos          bool
	disableAdoRepos       bool
	rewirePipelines       bool
	downloadMigrationLogs bool
	all                   bool
	repoList              string
}

// ---------------------------------------------------------------------------
// Options (derived from flags)
// ---------------------------------------------------------------------------

type generateScriptOptions struct {
	createTeams           bool
	linkIdpGroups         bool
	lockAdoRepos          bool
	disableAdoRepos       bool
	rewirePipelines       bool
	downloadMigrationLogs bool
}

func deriveOptions(a generateScriptArgs) generateScriptOptions {
	return generateScriptOptions{
		createTeams:           a.all || a.createTeams || a.linkIdpGroups,
		linkIdpGroups:         a.all || a.linkIdpGroups,
		lockAdoRepos:          a.all || a.lockAdoRepos,
		disableAdoRepos:       a.all || a.disableAdoRepos,
		rewirePipelines:       a.all || a.rewirePipelines,
		downloadMigrationLogs: a.all || a.downloadMigrationLogs,
	}
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newGenerateScriptCmd(
	adoAPI generateScriptAdoAPI,
	inspector generateScriptInspector,
	log *logger.Logger,
	writeToFile func(path, content string) error,
) *cobra.Command {
	var a generateScriptArgs

	cmd := &cobra.Command{
		Use:   "generate-script",
		Short: "Generates a migration script",
		Long:  "Generates a PowerShell script that automates an Azure DevOps to GitHub migration.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runGenerateScript(cmd.Context(), adoAPI, inspector, log, a, writeToFile)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "Azure DevOps organization name")
	cmd.Flags().StringVar(&a.adoTeamProject, "ado-team-project", "", "Azure DevOps team project name")
	cmd.Flags().StringVar(&a.output, "output", "./migrate.ps1", "Output file path for the migration script")
	cmd.Flags().BoolVar(&a.sequential, "sequential", false, "Generate a sequential (non-parallel) script")
	cmd.Flags().StringVar(&a.adoServerURL, "ado-server-url", "", "Azure DevOps Server URL")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().BoolVar(&a.createTeams, "create-teams", false, "Include team creation and assignment scripts")
	cmd.Flags().BoolVar(&a.linkIdpGroups, "link-idp-groups", false, "Link IdP groups to teams")
	cmd.Flags().BoolVar(&a.lockAdoRepos, "lock-ado-repos", false, "Lock ADO repos before migration")
	cmd.Flags().BoolVar(&a.disableAdoRepos, "disable-ado-repos", false, "Disable ADO repos after migration")
	cmd.Flags().BoolVar(&a.rewirePipelines, "rewire-pipelines", false, "Rewire Azure Pipelines to GitHub repos")
	cmd.Flags().BoolVar(&a.downloadMigrationLogs, "download-migration-logs", false, "Download migration logs after migration")
	cmd.Flags().BoolVar(&a.all, "all", false, "Enable all optional migration steps")
	cmd.Flags().StringVar(&a.repoList, "repo-list", "", "Path to a CSV file with repos to migrate")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("ado-server-url")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newGenerateScriptCmdLive() *cobra.Command { //nolint:unused // will be wired into main.go
	// TODO: wire up real ADO client and inspector
	return &cobra.Command{
		Use:   "generate-script",
		Short: "Generates a migration script",
	}
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runGenerateScript(
	ctx context.Context,
	adoAPI generateScriptAdoAPI,
	inspector generateScriptInspector,
	log *logger.Logger,
	a generateScriptArgs,
	writeToFile func(path, content string) error,
) error {
	log.Info("Generating Script...")

	opts := deriveOptions(a)

	if strings.TrimSpace(a.repoList) != "" {
		log.Info("Loading Repo CSV File...")
		if err := inspector.LoadReposCsv(a.repoList); err != nil {
			return err
		}
	}

	repoCount, err := inspector.GetRepoCount(ctx)
	if err != nil {
		return err
	}
	if repoCount == 0 {
		log.Errorf("A migration script could not be generated because no migratable repos were found. Please note that the GEI does not migrate disabled or TFVC repos.")
		return nil
	}

	var appIDs map[string]string
	if opts.rewirePipelines {
		appIDs, err = getAppIDs(ctx, adoAPI, inspector, log, a.githubOrg)
		if err != nil {
			return err
		}
	} else {
		appIDs = make(map[string]string)
	}

	var script string
	if a.sequential {
		script, err = generateSequentialScript(ctx, inspector, log, opts, appIDs, a.githubOrg, a.adoServerURL, a.targetAPIURL)
	} else {
		script, err = generateParallelScript(ctx, inspector, log, opts, appIDs, a.githubOrg, a.adoServerURL, a.targetAPIURL)
	}
	if err != nil {
		return err
	}

	inspector.OutputRepoListToLog()

	if err := checkForDuplicateRepoNames(ctx, inspector, log); err != nil {
		return err
	}

	if strings.TrimSpace(a.output) != "" {
		return writeToFile(a.output, script)
	}

	return nil
}

// ---------------------------------------------------------------------------
// AppIDs
// ---------------------------------------------------------------------------

func getAppIDs(
	ctx context.Context,
	adoAPI generateScriptAdoAPI,
	inspector generateScriptInspector,
	log *logger.Logger,
	githubOrg string,
) (map[string]string, error) {
	appIDs := make(map[string]string)

	orgs, err := inspector.GetOrgs(ctx)
	if err != nil {
		return nil, err
	}

	for _, org := range orgs {
		// Not using inspector here — we want ALL team projects, even when filtering.
		teamProjects, err := adoAPI.GetTeamProjects(ctx, org)
		if err != nil {
			return nil, err
		}

		appID, err := adoAPI.GetGithubAppId(ctx, org, githubOrg, teamProjects)
		if err != nil {
			return nil, err
		}

		if strings.TrimSpace(appID) != "" {
			appIDs[org] = appID
		} else {
			log.Warning("CANNOT FIND GITHUB APP SERVICE CONNECTION IN ADO ORGANIZATION: %s. You must install the Pipelines app in GitHub and connect it to any Team Project in this ADO Org first.", org)
		}
	}

	return appIDs, nil
}

// ---------------------------------------------------------------------------
// Duplicate check
// ---------------------------------------------------------------------------

func checkForDuplicateRepoNames(ctx context.Context, inspector generateScriptInspector, log *logger.Logger) error {
	seen := make(map[string]bool)

	orgs, err := inspector.GetOrgs(ctx)
	if err != nil {
		return err
	}

	for _, org := range orgs {
		teamProjects, err := inspector.GetTeamProjects(ctx, org)
		if err != nil {
			return err
		}
		for _, tp := range teamProjects {
			repos, err := inspector.GetRepos(ctx, org, tp)
			if err != nil {
				return err
			}
			for _, repo := range repos {
				ghName := getGithubRepoName(tp, repo.Name)
				if seen[ghName] {
					log.Warning("DUPLICATE REPO NAME: %s", ghName)
				} else {
					seen[ghName] = true
				}
			}
		}
	}

	return nil
}

// ---------------------------------------------------------------------------
// Sequential script
// ---------------------------------------------------------------------------

func generateSequentialScript(
	ctx context.Context,
	inspector generateScriptInspector,
	log *logger.Logger,
	opts generateScriptOptions,
	appIDs map[string]string,
	githubOrg, adoServerURL, targetAPIURL string,
) (string, error) {
	var sb strings.Builder

	appendLine(&sb, scriptgen.PwshShebang)
	appendBlankLine(&sb)
	appendLine(&sb, versionComment())
	appendLine(&sb, scriptgen.ExecFunctionBlock)
	appendLine(&sb, scriptgen.ValidateADOEnvVars)

	orgs, err := inspector.GetOrgs(ctx)
	if err != nil {
		return "", err
	}

	for _, adoOrg := range orgs {
		appendLine(&sb, fmt.Sprintf("# =========== Organization: %s ===========", adoOrg))

		appID := appIDs[adoOrg]

		if opts.rewirePipelines && appID == "" {
			appendLine(&sb, "# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos")
		}

		teamProjects, err := inspector.GetTeamProjects(ctx, adoOrg)
		if err != nil {
			return "", err
		}

		for _, adoTP := range teamProjects {
			appendBlankLine(&sb)
			appendLine(&sb, fmt.Sprintf("# === Team Project: %s/%s ===", adoOrg, adoTP))

			repos, err := inspector.GetRepos(ctx, adoOrg, adoTP)
			if err != nil {
				return "", err
			}

			if len(repos) == 0 {
				appendLine(&sb, "# Skipping this Team Project because it has no git repos")
				continue
			}

			appendLine(&sb, execWrap(createGithubMaintainersTeamScript(adoTP, githubOrg, opts.createTeams, opts.linkIdpGroups, targetAPIURL, log.IsVerbose())))
			appendLine(&sb, execWrap(createGithubAdminsTeamScript(adoTP, githubOrg, opts.createTeams, opts.linkIdpGroups, targetAPIURL, log.IsVerbose())))
			appendLine(&sb, execWrap(shareServiceConnectionScript(adoOrg, adoTP, appID, opts.rewirePipelines, log.IsVerbose())))

			for _, repo := range repos {
				githubRepo := getGithubRepoName(adoTP, repo.Name)

				appendBlankLine(&sb)
				appendLine(&sb, execWrap(lockAdoRepoScript(adoOrg, adoTP, repo.Name, opts.lockAdoRepos, log.IsVerbose())))
				appendLine(&sb, execWrap(migrateRepoScript(adoOrg, adoTP, repo.Name, githubOrg, githubRepo, true, adoServerURL, targetAPIURL, log.IsVerbose())))
				appendLine(&sb, execWrap(disableAdoRepoScript(adoOrg, adoTP, repo.Name, opts.disableAdoRepos, log.IsVerbose())))
				appendLine(&sb, execWrap(addMaintainersToGithubRepoScript(adoTP, githubOrg, githubRepo, targetAPIURL, opts.createTeams, log.IsVerbose())))
				appendLine(&sb, execWrap(addAdminsToGithubRepoScript(adoTP, githubOrg, githubRepo, targetAPIURL, opts.createTeams, log.IsVerbose())))
				appendLine(&sb, execWrap(downloadMigrationLogScript(githubOrg, githubRepo, targetAPIURL, opts.downloadMigrationLogs)))

				pipelines, err := inspector.GetPipelines(ctx, adoOrg, adoTP, repo.Name)
				if err != nil {
					return "", err
				}
				for _, pipeline := range pipelines {
					appendLine(&sb, execWrap(rewireAzurePipelineScript(adoOrg, adoTP, pipeline, githubOrg, githubRepo, appID, opts.rewirePipelines, log.IsVerbose())))
				}
			}
		}

		appendBlankLine(&sb)
		appendBlankLine(&sb)
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// Parallel script
// ---------------------------------------------------------------------------

func generateParallelScript(
	ctx context.Context,
	inspector generateScriptInspector,
	log *logger.Logger,
	opts generateScriptOptions,
	appIDs map[string]string,
	githubOrg, adoServerURL, targetAPIURL string,
) (string, error) {
	var sb strings.Builder

	appendLine(&sb, scriptgen.PwshShebang)
	appendBlankLine(&sb)
	appendLine(&sb, versionComment())
	appendLine(&sb, scriptgen.ExecFunctionBlock)
	appendLine(&sb, scriptgen.ExecAndGetMigrationIDFunctionBlock)
	appendLine(&sb, scriptgen.ExecBatchFunctionBlock)
	appendLine(&sb, scriptgen.ValidateADOEnvVars)

	appendBlankLine(&sb)
	appendLine(&sb, "$Succeeded = 0")
	appendLine(&sb, "$Failed = 0")
	appendLine(&sb, "$RepoMigrations = [ordered]@{}")

	orgs, err := inspector.GetOrgs(ctx)
	if err != nil {
		return "", err
	}

	if err := parallelQueuePhase(ctx, &sb, inspector, log, opts, appIDs, orgs, githubOrg, adoServerURL, targetAPIURL); err != nil {
		return "", err
	}

	if err := parallelWaitPhase(ctx, &sb, inspector, log, opts, appIDs, orgs, githubOrg, targetAPIURL); err != nil {
		return "", err
	}

	// Summary
	appendBlankLine(&sb)
	appendLine(&sb, "Write-Host =============== Summary ===============")
	appendLine(&sb, "Write-Host Total number of successful migrations: $Succeeded")
	appendLine(&sb, "Write-Host Total number of failed migrations: $Failed")
	appendLine(&sb, "\nif ($Failed -ne 0) {\n    exit 1\n}")
	appendBlankLine(&sb)
	appendBlankLine(&sb)

	return sb.String(), nil
}

func parallelQueuePhase(
	ctx context.Context,
	sb *strings.Builder,
	inspector generateScriptInspector,
	log *logger.Logger,
	opts generateScriptOptions,
	appIDs map[string]string,
	orgs []string,
	githubOrg, adoServerURL, targetAPIURL string,
) error {
	for _, adoOrg := range orgs {
		appendBlankLine(sb)
		appendLine(sb, fmt.Sprintf("# =========== Queueing migration for Organization: %s ===========", adoOrg))

		appID := appIDs[adoOrg]

		if opts.rewirePipelines && appID == "" {
			appendBlankLine(sb)
			appendLine(sb, "# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos")
		}

		teamProjects, err := inspector.GetTeamProjects(ctx, adoOrg)
		if err != nil {
			return err
		}

		for _, adoTP := range teamProjects {
			appendBlankLine(sb)
			appendLine(sb, fmt.Sprintf("# === Queueing repo migrations for Team Project: %s/%s ===", adoOrg, adoTP))

			repos, err := inspector.GetRepos(ctx, adoOrg, adoTP)
			if err != nil {
				return err
			}

			if len(repos) == 0 {
				appendLine(sb, "# Skipping this Team Project because it has no git repos")
				continue
			}

			appendLine(sb, execWrap(createGithubMaintainersTeamScript(adoTP, githubOrg, opts.createTeams, opts.linkIdpGroups, targetAPIURL, log.IsVerbose())))
			appendLine(sb, execWrap(createGithubAdminsTeamScript(adoTP, githubOrg, opts.createTeams, opts.linkIdpGroups, targetAPIURL, log.IsVerbose())))
			appendLine(sb, execWrap(shareServiceConnectionScript(adoOrg, adoTP, appID, opts.rewirePipelines, log.IsVerbose())))

			for _, repo := range repos {
				githubRepo := getGithubRepoName(adoTP, repo.Name)

				appendBlankLine(sb)
				appendLine(sb, execWrap(lockAdoRepoScript(adoOrg, adoTP, repo.Name, opts.lockAdoRepos, log.IsVerbose())))
				appendLine(sb, queueMigrateRepoScript(adoOrg, adoTP, repo.Name, githubOrg, githubRepo, adoServerURL, targetAPIURL, log.IsVerbose()))
				appendLine(sb, fmt.Sprintf(`$RepoMigrations["%s"] = $MigrationID`, getRepoMigrationKey(adoOrg, githubRepo)))
			}
		}
	}
	return nil
}

func parallelWaitPhase(
	ctx context.Context,
	sb *strings.Builder,
	inspector generateScriptInspector,
	log *logger.Logger,
	opts generateScriptOptions,
	appIDs map[string]string,
	orgs []string,
	githubOrg, targetAPIURL string,
) error {
	for _, adoOrg := range orgs {
		appendBlankLine(sb)
		appendLine(sb, fmt.Sprintf("# =========== Waiting for all migrations to finish for Organization: %s ===========", adoOrg))

		teamProjects, err := inspector.GetTeamProjects(ctx, adoOrg)
		if err != nil {
			return err
		}

		for _, adoTP := range teamProjects {
			repos, err := inspector.GetRepos(ctx, adoOrg, adoTP)
			if err != nil {
				return err
			}

			for _, repo := range repos {
				appendBlankLine(sb)
				appendLine(sb, fmt.Sprintf("# === Waiting for repo migration to finish for Team Project: %s and Repo: %s. Will then complete the below post migration steps. ===", adoTP, repo.Name))

				githubRepo := getGithubRepoName(adoTP, repo.Name)
				repoMigKey := getRepoMigrationKey(adoOrg, githubRepo)

				appendLine(sb, "$CanExecuteBatch = $false")
				appendLine(sb, fmt.Sprintf(`if ($null -ne $RepoMigrations["%s"]) {`, repoMigKey))
				appendLine(sb, "    "+waitForMigrationScript(repoMigKey, targetAPIURL))
				appendLine(sb, "    $CanExecuteBatch = ($lastexitcode -eq 0)")
				appendLine(sb, "}")
				appendLine(sb, "if ($CanExecuteBatch) {")

				needsBatch := opts.createTeams || opts.disableAdoRepos || opts.rewirePipelines || opts.downloadMigrationLogs
				if needsBatch {
					appendLine(sb, "    ExecBatch @(")
					appendLine(sb, "        "+wrap(disableAdoRepoScript(adoOrg, adoTP, repo.Name, opts.disableAdoRepos, log.IsVerbose())))
					appendLine(sb, "        "+wrap(addMaintainersToGithubRepoScript(adoTP, githubOrg, githubRepo, targetAPIURL, opts.createTeams, log.IsVerbose())))
					appendLine(sb, "        "+wrap(addAdminsToGithubRepoScript(adoTP, githubOrg, githubRepo, targetAPIURL, opts.createTeams, log.IsVerbose())))
					appendLine(sb, "        "+wrap(downloadMigrationLogScript(githubOrg, githubRepo, targetAPIURL, opts.downloadMigrationLogs)))

					appID := appIDs[adoOrg]
					pipelines, err := inspector.GetPipelines(ctx, adoOrg, adoTP, repo.Name)
					if err != nil {
						return err
					}
					for _, pipeline := range pipelines {
						appendLine(sb, "        "+wrap(rewireAzurePipelineScript(adoOrg, adoTP, pipeline, githubOrg, githubRepo, appID, opts.rewirePipelines, log.IsVerbose())))
					}

					appendLine(sb, "    )")
					appendLine(sb, "    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }")
				} else {
					appendLine(sb, "    $Succeeded++")
				}

				appendLine(sb, "} else {")
				appendLine(sb, "    $Failed++")
				appendLine(sb, "}")
			}
		}
	}
	return nil
}

// ---------------------------------------------------------------------------
// Script command helpers
// ---------------------------------------------------------------------------

func migrateRepoScript(adoOrg, adoTP, adoRepo, githubOrg, githubRepo string, wait bool, adoServerURL, targetAPIURL string, verbose bool) string {
	var sb strings.Builder
	sb.WriteString("gh ado2gh migrate-repo")
	if strings.TrimSpace(targetAPIURL) != "" {
		fmt.Fprintf(&sb, ` --target-api-url "%s"`, targetAPIURL)
	}
	fmt.Fprintf(&sb, ` --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s"`, adoOrg, adoTP, adoRepo, githubOrg, githubRepo)
	if verbose {
		sb.WriteString(verboseFlag)
	}
	if !wait {
		sb.WriteString(" --queue-only")
	}
	sb.WriteString(" --target-repo-visibility private")
	if strings.TrimSpace(adoServerURL) != "" {
		fmt.Fprintf(&sb, ` --ado-server-url "%s"`, adoServerURL)
	}
	return sb.String()
}

func queueMigrateRepoScript(adoOrg, adoTP, adoRepo, githubOrg, githubRepo, adoServerURL, targetAPIURL string, verbose bool) string {
	inner := migrateRepoScript(adoOrg, adoTP, adoRepo, githubOrg, githubRepo, false, adoServerURL, targetAPIURL, verbose)
	return fmt.Sprintf("$MigrationID = ExecAndGetMigrationID { %s }", inner)
}

func createGithubMaintainersTeamScript(adoTP, githubOrg string, createTeams, linkIdpGroups bool, targetAPIURL string, verbose bool) string {
	if !createTeams {
		return ""
	}
	return createTeamScript(adoTP, githubOrg, "Maintainers", linkIdpGroups, targetAPIURL, verbose)
}

func createGithubAdminsTeamScript(adoTP, githubOrg string, createTeams, linkIdpGroups bool, targetAPIURL string, verbose bool) string {
	if !createTeams {
		return ""
	}
	return createTeamScript(adoTP, githubOrg, "Admins", linkIdpGroups, targetAPIURL, verbose)
}

func createTeamScript(adoTP, githubOrg, suffix string, linkIdpGroups bool, targetAPIURL string, verbose bool) string {
	teamName := ado.ReplaceInvalidCharactersWithDash(adoTP) + "-" + suffix

	var sb strings.Builder
	sb.WriteString("gh ado2gh create-team")
	if strings.TrimSpace(targetAPIURL) != "" {
		fmt.Fprintf(&sb, ` --target-api-url "%s"`, targetAPIURL)
	}
	fmt.Fprintf(&sb, ` --github-org "%s" --team-name "%s"`, githubOrg, teamName)
	if verbose {
		sb.WriteString(verboseFlag)
	}
	if linkIdpGroups {
		fmt.Fprintf(&sb, ` --idp-group "%s"`, teamName)
	}
	return sb.String()
}

func addMaintainersToGithubRepoScript(adoTP, githubOrg, githubRepo, targetAPIURL string, createTeams, verbose bool) string {
	if !createTeams {
		return ""
	}
	return addTeamToRepoScript(adoTP, githubOrg, githubRepo, "Maintainers", "maintain", targetAPIURL, verbose)
}

func addAdminsToGithubRepoScript(adoTP, githubOrg, githubRepo, targetAPIURL string, createTeams, verbose bool) string {
	if !createTeams {
		return ""
	}
	return addTeamToRepoScript(adoTP, githubOrg, githubRepo, "Admins", "admin", targetAPIURL, verbose)
}

func addTeamToRepoScript(adoTP, githubOrg, githubRepo, suffix, role, targetAPIURL string, verbose bool) string {
	teamName := ado.ReplaceInvalidCharactersWithDash(adoTP) + "-" + suffix

	var sb strings.Builder
	sb.WriteString("gh ado2gh add-team-to-repo")
	if strings.TrimSpace(targetAPIURL) != "" {
		fmt.Fprintf(&sb, ` --target-api-url "%s"`, targetAPIURL)
	}
	fmt.Fprintf(&sb, ` --github-org "%s" --github-repo "%s" --team "%s" --role "%s"`, githubOrg, githubRepo, teamName, role)
	if verbose {
		sb.WriteString(verboseFlag)
	}
	return sb.String()
}

func shareServiceConnectionScript(adoOrg, adoTP, appID string, rewirePipelines, verbose bool) string {
	if !rewirePipelines || strings.TrimSpace(appID) == "" {
		return ""
	}
	s := fmt.Sprintf(`gh ado2gh share-service-connection --ado-org "%s" --ado-team-project "%s" --service-connection-id "%s"`, adoOrg, adoTP, appID)
	if verbose {
		s += verboseFlag
	}
	return s
}

func lockAdoRepoScript(adoOrg, adoTP, adoRepo string, lockAdoRepos, verbose bool) string {
	if !lockAdoRepos {
		return ""
	}
	s := fmt.Sprintf(`gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s"`, adoOrg, adoTP, adoRepo)
	if verbose {
		s += verboseFlag
	}
	return s
}

func disableAdoRepoScript(adoOrg, adoTP, adoRepo string, disableAdoRepos, verbose bool) string {
	if !disableAdoRepos {
		return ""
	}
	s := fmt.Sprintf(`gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s"`, adoOrg, adoTP, adoRepo)
	if verbose {
		s += verboseFlag
	}
	return s
}

func rewireAzurePipelineScript(adoOrg, adoTP, adoPipeline, githubOrg, githubRepo, appID string, rewirePipelines, verbose bool) string {
	if !rewirePipelines || strings.TrimSpace(appID) == "" {
		return ""
	}
	s := fmt.Sprintf(`gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s" --service-connection-id "%s"`, adoOrg, adoTP, adoPipeline, githubOrg, githubRepo, appID)
	if verbose {
		s += verboseFlag
	}
	return s
}

func waitForMigrationScript(repoMigrationKey, targetAPIURL string) string {
	var sb strings.Builder
	sb.WriteString("gh ado2gh wait-for-migration")
	if strings.TrimSpace(targetAPIURL) != "" {
		fmt.Fprintf(&sb, ` --target-api-url "%s"`, targetAPIURL)
	}
	fmt.Fprintf(&sb, ` --migration-id $RepoMigrations["%s"]`, repoMigrationKey)
	return sb.String()
}

func downloadMigrationLogScript(githubOrg, githubRepo, targetAPIURL string, downloadMigrationLogs bool) string {
	if !downloadMigrationLogs {
		return ""
	}
	var sb strings.Builder
	sb.WriteString("gh ado2gh download-logs")
	if strings.TrimSpace(targetAPIURL) != "" {
		fmt.Fprintf(&sb, ` --target-api-url "%s"`, targetAPIURL)
	}
	fmt.Fprintf(&sb, ` --github-org "%s" --github-repo "%s"`, githubOrg, githubRepo)
	return sb.String()
}

// ---------------------------------------------------------------------------
// String helpers
// ---------------------------------------------------------------------------

func getGithubRepoName(adoTeamProject, repo string) string {
	return ado.ReplaceInvalidCharactersWithDash(adoTeamProject + "-" + repo)
}

func getRepoMigrationKey(adoOrg, githubRepoName string) string {
	return adoOrg + "/" + githubRepoName
}

func versionComment() string {
	return fmt.Sprintf("# =========== Created with CLI version %s ===========", version)
}

// appendLine appends content + newline, but SKIPS if content is empty/whitespace.
func appendLine(sb *strings.Builder, content string) {
	if strings.TrimSpace(content) == "" {
		return
	}
	sb.WriteString(content)
	sb.WriteByte('\n')
}

// appendBlankLine always appends a newline (equivalent to C# AppendLine() with no args).
func appendBlankLine(sb *strings.Builder) {
	sb.WriteByte('\n')
}

// execWrap wraps a script in "Exec { ... }". Returns "" if script is empty.
func execWrap(script string) string {
	if strings.TrimSpace(script) == "" {
		return ""
	}
	return fmt.Sprintf("Exec { %s }", script)
}

// wrap wraps a script in "{ ... }". Returns "" if script is empty.
func wrap(script string) string {
	if strings.TrimSpace(script) == "" {
		return ""
	}
	return fmt.Sprintf("{ %s }", script)
}

// defaultWriteToFile writes content to a file (production implementation).
func defaultWriteToFile(path, content string) error { //nolint:unused // used by newGenerateScriptCmdLive
	return os.WriteFile(path, []byte(content), 0o600)
}
