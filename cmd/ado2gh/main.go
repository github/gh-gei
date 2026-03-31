package main

import (
	"context"
	"net/http"
	"os"
	"strings"

	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/status"
	versionpkg "github.com/github/gh-gei/pkg/version"
	"github.com/spf13/cobra"
)

// contextKey is an unexported type for context keys in this package.
type contextKey string

const loggerKey contextKey = "logger"

var (
	version = "dev"
	verbose bool
)

func main() {
	if err := newRootCmd().Execute(); err != nil {
		os.Exit(1)
	}
}

func newRootCmd() *cobra.Command {
	rootCmd := &cobra.Command{
		Use:   "ado2gh",
		Short: "Azure DevOps to GitHub migration CLI",
		Long:  "Automate end-to-end Azure DevOps Repos to GitHub migrations.",
		PersistentPreRunE: func(cmd *cobra.Command, args []string) error {
			log := logger.New(verbose)
			ctx := context.WithValue(cmd.Context(), loggerKey, log)
			cmd.SetContext(ctx)
			log.Debug("Execution started")

			checkVersion(ctx, log)
			checkGitHubStatus(ctx, log)

			return nil
		},
		SilenceUsage:  true,
		SilenceErrors: true,
	}

	rootCmd.PersistentFlags().BoolVarP(&verbose, "verbose", "v", false, "Enable verbose logging")
	rootCmd.Version = version

	// Add commands (will be implemented in phases)
	rootCmd.AddCommand(newMigrateRepoCmdLive())
	rootCmd.AddCommand(newGenerateScriptCmdLive())
	// rootCmd.AddCommand(newInventoryReportCmd())
	// rootCmd.AddCommand(newRewirePipelineCmd())
	// rootCmd.AddCommand(newIntegrateBoardsCmd())
	// rootCmd.AddCommand(newAddTeamToRepoCmd())
	// rootCmd.AddCommand(newLockRepoCmd())
	// rootCmd.AddCommand(newDisableRepoCmd())
	// rootCmd.AddCommand(newConfigureAutoLinkCmd())
	// rootCmd.AddCommand(newShareServiceConnectionCmd())
	// rootCmd.AddCommand(newTestPipelinesCmd())
	// Shared commands from gei
	// rootCmd.AddCommand(newWaitForMigrationCmd())
	// rootCmd.AddCommand(newAbortMigrationCmd())
	// rootCmd.AddCommand(newDownloadLogsCmd())
	// rootCmd.AddCommand(newGenerateMannequinCSVCmd())
	// rootCmd.AddCommand(newReclaimMannequinCmd())
	// rootCmd.AddCommand(newGrantMigratorRoleCmd())
	// rootCmd.AddCommand(newRevokeMigratorRoleCmd())
	// rootCmd.AddCommand(newCreateTeamCmd())

	return rootCmd
}

func getLogger(cmd *cobra.Command) *logger.Logger {
	if log, ok := cmd.Context().Value(loggerKey).(*logger.Logger); ok {
		return log
	}
	return logger.New(false)
}

func getEnvProvider() *env.Provider {
	return env.New()
}

func checkVersion(ctx context.Context, log *logger.Logger) {
	envProvider := getEnvProvider()
	skip := envProvider.SkipVersionCheck()
	if strings.EqualFold(skip, "true") || skip == "1" {
		log.Info("Skipped latest version check due to GEI_SKIP_VERSION_CHECK environment variable")
		return
	}

	checker := versionpkg.NewChecker(&http.Client{}, log, version)
	isLatest, err := checker.IsLatest(ctx)
	if err != nil {
		log.Debug("Version check failed: %v", err)
		return
	}

	if !isLatest {
		latest, _ := checker.GetLatestVersion(ctx)
		log.Info("New version available: %s", latest)
		log.Info("You are running ado2gh CLI version %s", version)
	}
}

func checkGitHubStatus(ctx context.Context, log *logger.Logger) {
	envProvider := getEnvProvider()
	skip := envProvider.SkipStatusCheck()
	if strings.EqualFold(skip, "true") || skip == "1" {
		log.Info("Skipped GitHub status check due to GEI_SKIP_STATUS_CHECK environment variable")
		return
	}

	count, err := status.GetUnresolvedIncidentsCount(ctx, &http.Client{}, "https://www.githubstatus.com")
	if err != nil {
		log.Debug("GitHub status check failed: %v", err)
		return
	}

	if count > 0 {
		log.Warning("GitHub is currently experiencing %d incident(s). Check https://www.githubstatus.com for details.", count)
	}
}
