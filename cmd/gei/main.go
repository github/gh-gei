package main

import (
	"context"
	"errors"
	"fmt"
	"net/http"
	"os"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
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
	rootCmd := newRootCmd()
	if err := rootCmd.Execute(); err != nil {
		// Retrieve logger from the command context if available
		if log, ok := rootCmd.Context().Value(loggerKey).(*logger.Logger); ok && log != nil {
			var userErr *cmdutil.UserError
			if errors.As(err, &userErr) {
				log.Errorf("%v", err)
			} else {
				log.Errorf("Unexpected error: %v", err)
			}
		} else {
			fmt.Fprintf(os.Stderr, "[ERROR] %v\n", err)
		}
		os.Exit(1)
	}
}

func newRootCmd() *cobra.Command {
	rootCmd := &cobra.Command{
		Use:   "gei",
		Short: "GitHub Enterprise Importer CLI",
		Long:  "CLI for migrating repositories between GitHub instances using GitHub Enterprise Importer.",
		PersistentPreRunE: func(cmd *cobra.Command, args []string) error {
			// Initialize logger
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

	// Add commands
	rootCmd.AddCommand(newGenerateScriptCmd())
	rootCmd.AddCommand(newMigrateRepoCmdLive())
	rootCmd.AddCommand(newMigrateOrgCmdLive())

	rootCmd.AddCommand(newMigrateSecretAlertsCmdLive())
	rootCmd.AddCommand(newMigrateCodeScanningCmdLive())

	rootCmd.AddCommand(newWaitForMigrationCmdLive())
	rootCmd.AddCommand(newAbortMigrationCmdLive())
	rootCmd.AddCommand(newDownloadLogsCmdLive())
	rootCmd.AddCommand(newGenerateMannequinCSVCmdLive())
	rootCmd.AddCommand(newReclaimMannequinCmdLive())
	rootCmd.AddCommand(newGrantMigratorRoleCmdLive())
	rootCmd.AddCommand(newRevokeMigratorRoleCmdLive())
	rootCmd.AddCommand(newCreateTeamCmdLive())

	return rootCmd
}

// getLogger retrieves the logger from the command context
func getLogger(cmd *cobra.Command) *logger.Logger {
	if log, ok := cmd.Context().Value(loggerKey).(*logger.Logger); ok {
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
		log.Info("You are running gei CLI version %s", version)
	}
}

// checkGitHubStatus checks if GitHub is experiencing incidents
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
