package main

import (
	"context"
	"fmt"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// logDownloader is the consumer-defined interface for fetching migration info.
type logDownloader interface {
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
	GetMigrationLogUrl(ctx context.Context, org, repo string) (*github.MigrationLogResult, error)
}

// fileDownloader is the consumer-defined interface for downloading files.
type fileDownloader interface {
	DownloadToFile(ctx context.Context, url, filepath string) error
}

// fileChecker is the consumer-defined interface for checking file existence.
type fileChecker interface {
	FileExists(path string) bool
}

// downloadLogsOptions holds tunable parameters for the download-logs command,
// allowing tests to set retries=0 and delay=0 so they don't wait.
type downloadLogsOptions struct {
	maxRetries int
	retryDelay time.Duration
}

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
			return runDownloadLogs(cmd.Context(), gh, dl, fc, log, downloadLogsParams{
				migrationID:     migrationID,
				githubTargetOrg: githubTargetOrg,
				targetRepo:      targetRepo,
				logFile:         logFile,
				overwrite:       overwrite,
				maxRetries:      opts.maxRetries,
				retryDelay:      opts.retryDelay,
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

type downloadLogsParams struct {
	migrationID     string
	githubTargetOrg string
	targetRepo      string
	logFile         string
	overwrite       bool
	maxRetries      int
	retryDelay      time.Duration
}

func runDownloadLogs(ctx context.Context, gh logDownloader, dl fileDownloader, fc fileChecker, log *logger.Logger, p downloadLogsParams) error {
	hasMigrationID := p.migrationID != ""
	hasOrgRepo := p.githubTargetOrg != "" && p.targetRepo != ""

	if !hasMigrationID && !hasOrgRepo {
		return cmdutil.NewUserError("must provide either --migration-id or both --github-target-org and --target-repo")
	}

	// Check custom filename early
	if p.logFile != "" {
		if err := checkFileOverwrite(fc, log, p.logFile, p.overwrite); err != nil {
			return err
		}
	}

	log.Warning("Migration logs are only available for 24 hours after a migration finishes!")

	var (
		logURL   string
		filename string
		repoName string
	)

	if hasMigrationID {
		if p.githubTargetOrg != "" || p.targetRepo != "" {
			log.Warning("--github-target-org and --target-repo will be ignored because --migration-id was provided")
		}

		m, err := waitForMigrationLogByID(ctx, gh, p.migrationID, p.maxRetries, p.retryDelay)
		if err != nil {
			return err
		}
		logURL = m.MigrationLogURL
		repoName = m.RepositoryName
		filename = fmt.Sprintf("migration-log-%s-%s.log", m.RepositoryName, p.migrationID)
	} else {
		result, err := waitForMigrationLogByOrgRepo(ctx, gh, p.githubTargetOrg, p.targetRepo, p.maxRetries, p.retryDelay)
		if err != nil {
			return err
		}
		logURL = result.MigrationLogURL
		repoName = p.targetRepo
		filename = fmt.Sprintf("migration-log-%s-%s-%s.log", p.githubTargetOrg, p.targetRepo, result.MigrationID)
	}

	if p.logFile != "" {
		filename = p.logFile
	} else {
		// Check default filename for overwrite
		if err := checkFileOverwrite(fc, log, filename, p.overwrite); err != nil {
			return err
		}
	}

	log.Info("Downloading migration logs...")
	log.Info("Downloading log for repository %s to %s...", repoName, filename)

	if err := dl.DownloadToFile(ctx, logURL, filename); err != nil {
		return err
	}

	log.Success("Downloaded %s log to %s.", repoName, filename)
	return nil
}

func waitForMigrationLogByID(ctx context.Context, gh logDownloader, migrationID string, maxRetries int, retryDelay time.Duration) (*github.Migration, error) {
	for attempt := 0; attempt <= maxRetries; attempt++ {
		m, err := gh.GetMigration(ctx, migrationID)
		if err != nil {
			return nil, err
		}
		if m.MigrationLogURL != "" {
			return m, nil
		}
		if attempt < maxRetries {
			select {
			case <-ctx.Done():
				return nil, ctx.Err()
			case <-time.After(retryDelay):
			}
		}
	}
	return nil, cmdutil.NewUserErrorf("migration log URL was not populated for migration %s after retries", migrationID)
}

func waitForMigrationLogByOrgRepo(ctx context.Context, gh logDownloader, org, repo string, maxRetries int, retryDelay time.Duration) (*github.MigrationLogResult, error) {
	for attempt := 0; attempt <= maxRetries; attempt++ {
		result, err := gh.GetMigrationLogUrl(ctx, org, repo)
		if err != nil {
			return nil, err
		}
		if result == nil {
			return nil, cmdutil.NewUserErrorf("no migration found for %s/%s", org, repo)
		}
		if result.MigrationLogURL != "" {
			return result, nil
		}
		if attempt < maxRetries {
			select {
			case <-ctx.Done():
				return nil, ctx.Err()
			case <-time.After(retryDelay):
			}
		}
	}
	return nil, cmdutil.NewUserErrorf("migration log URL was not populated for %s/%s after retries", org, repo)
}

func checkFileOverwrite(fc fileChecker, log *logger.Logger, filepath string, overwrite bool) error {
	if !fc.FileExists(filepath) {
		return nil
	}
	if !overwrite {
		return cmdutil.NewUserErrorf("file %s already exists. Use --overwrite to overwrite it", filepath)
	}
	log.Warning("File %s already exists and will be overwritten", filepath)
	return nil
}
