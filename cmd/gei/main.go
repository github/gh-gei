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
		Use:   "gei",
		Short: "GitHub Enterprise Importer CLI",
		Long:  "CLI for migrating repositories between GitHub instances using GitHub Enterprise Importer.",
		PersistentPreRun: func(cmd *cobra.Command, args []string) {
			// Initialize logger
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

	// Add commands
	rootCmd.AddCommand(newGenerateScriptCmd())

	// Additional commands will be implemented in subsequent phases
	// rootCmd.AddCommand(newMigrateRepoCmd())
	// rootCmd.AddCommand(newMigrateOrgCmd())
	// rootCmd.AddCommand(newWaitForMigrationCmd())
	// rootCmd.AddCommand(newAbortMigrationCmd())
	// rootCmd.AddCommand(newDownloadLogsCmd())
	// rootCmd.AddCommand(newMigrateSecretAlertsCmd())
	// rootCmd.AddCommand(newMigrateCodeScanningAlertsCmd())
	// rootCmd.AddCommand(newGenerateMannequinCSVCmd())
	// rootCmd.AddCommand(newReclaimMannequinCmd())
	// rootCmd.AddCommand(newGrantMigratorRoleCmd())
	// rootCmd.AddCommand(newRevokeMigratorRoleCmd())
	// rootCmd.AddCommand(newCreateTeamCmd())

	return rootCmd
}

// getLogger retrieves the logger from the command context
func getLogger(cmd *cobra.Command) *logger.Logger {
	if log, ok := cmd.Context().Value("logger").(*logger.Logger); ok {
		return log
	}
	return logger.New(false)
}

// getEnvProvider returns an environment provider
func getEnvProvider() *env.Provider {
	return env.New()
}

// checkVersion checks if a newer version is available
func checkVersion(ctx context.Context, log *logger.Logger) {
	envProvider := getEnvProvider()

	if envProvider.SkipVersionCheck() == "true" || envProvider.SkipVersionCheck() == "1" {
		log.Info("Skipped latest version check due to GEI_SKIP_VERSION_CHECK environment variable")
		return
	}

	// TODO: Implement version check
	log.Info("You are running gei CLI version %s", version)
}

// checkGitHubStatus checks if GitHub is experiencing incidents
func checkGitHubStatus(ctx context.Context, log *logger.Logger) {
	envProvider := getEnvProvider()

	if envProvider.SkipStatusCheck() == "true" || envProvider.SkipStatusCheck() == "1" {
		log.Info("Skipped GitHub status check due to GEI_SKIP_STATUS_CHECK environment variable")
		return
	}

	// TODO: Implement GitHub status check
}
