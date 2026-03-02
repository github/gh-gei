package main

import (
	"context"
	"os"

	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

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
		PersistentPreRun: func(cmd *cobra.Command, args []string) {
			log := logger.New(verbose)
			ctx := context.WithValue(cmd.Context(), "logger", log)
			cmd.SetContext(ctx)
			log.Debug("Execution started")
		},
		SilenceUsage:  true,
		SilenceErrors: true,
	}

	rootCmd.PersistentFlags().BoolVarP(&verbose, "verbose", "v", false, "Enable verbose logging")
	rootCmd.Version = version

	// Add commands (will be implemented in phases)
	// rootCmd.AddCommand(newMigrateRepoCmd())
	// rootCmd.AddCommand(newGenerateScriptCmd())
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
	if log, ok := cmd.Context().Value("logger").(*logger.Logger); ok {
		return log
	}
	return logger.New(false)
}

func getEnvProvider() *env.Provider {
	return env.New()
}

func checkVersion(ctx context.Context, log *logger.Logger) {
	envProvider := getEnvProvider()
	if envProvider.SkipVersionCheck() == "true" || envProvider.SkipVersionCheck() == "1" {
		log.Info("Skipped latest version check due to GEI_SKIP_VERSION_CHECK environment variable")
		return
	}
	log.Info("You are running ado2gh CLI version %s", version)
}

func checkGitHubStatus(ctx context.Context, log *logger.Logger) {
	envProvider := getEnvProvider()
	if envProvider.SkipStatusCheck() == "true" || envProvider.SkipStatusCheck() == "1" {
		log.Info("Skipped GitHub status check due to GEI_SKIP_STATUS_CHECK environment variable")
		return
	}
}
