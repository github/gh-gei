package main

import (
	"context"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// logDownloader is the consumer-defined interface for fetching migration info.
type logDownloader = sharedcmd.LogDownloader

// fileDownloader is the consumer-defined interface for downloading files.
type fileDownloader = sharedcmd.FileDownloader

// fileChecker is the consumer-defined interface for checking file existence.
type fileChecker = sharedcmd.FileChecker

// downloadLogsOptions holds tunable parameters for the download-logs command.
type downloadLogsOptions = sharedcmd.DownloadLogsOptions

// downloadLogsParams holds the parameters for the download-logs command.
type downloadLogsParams = sharedcmd.DownloadLogsParams

// newDownloadLogsCmd creates the download-logs cobra command.
func newDownloadLogsCmd(gh logDownloader, dl fileDownloader, fc fileChecker, log *logger.Logger, opts downloadLogsOptions) *cobra.Command {
	var (
		migrationID     string
		githubTargetOrg string
		targetRepo      string
		logFile         string
		overwrite       bool
	)

	cmd := &cobra.Command{
		Use:   "download-logs",
		Short: "Downloads migration logs for a repository migration",
		Long:  "Downloads migration logs for a repository migration, either by migration ID or by org/repo.",
		RunE: func(cmd *cobra.Command, args []string) error {
			return sharedcmd.RunDownloadLogs(cmd.Context(), gh, dl, fc, log, sharedcmd.DownloadLogsParams{
				MigrationID:     migrationID,
				GithubTargetOrg: githubTargetOrg,
				TargetRepo:      targetRepo,
				LogFile:         logFile,
				Overwrite:       overwrite,
				MaxRetries:      opts.MaxRetries,
				RetryDelay:      opts.RetryDelay,
			})
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "", "The ID of the migration")
	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "Target GitHub organization")
	cmd.Flags().StringVar(&targetRepo, "target-repo", "", "Target repository name")
	cmd.Flags().StringVar(&logFile, "migration-log-file", "", "Custom output filename for the migration log")
	cmd.Flags().BoolVar(&overwrite, "overwrite", false, "Overwrite the log file if it already exists")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// runDownloadLogs delegates to sharedcmd for backward compat with tests.
func runDownloadLogs(ctx context.Context, gh logDownloader, dl fileDownloader, fc fileChecker, log *logger.Logger, p downloadLogsParams) error {
	return sharedcmd.RunDownloadLogs(ctx, gh, dl, fc, log, p)
}
