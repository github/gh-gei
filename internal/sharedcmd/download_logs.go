package sharedcmd

import (
	"context"
	"fmt"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
)

// LogDownloader is the consumer-defined interface for fetching migration info.
type LogDownloader interface {
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
	GetMigrationLogUrl(ctx context.Context, org, repo string) (*github.MigrationLogResult, error)
}

// FileDownloader is the consumer-defined interface for downloading files.
type FileDownloader interface {
	DownloadToFile(ctx context.Context, url, filepath string) error
}

// FileChecker is the consumer-defined interface for checking file existence.
type FileChecker interface {
	FileExists(path string) bool
}

// DownloadLogsOptions holds tunable parameters for the download-logs command,
// allowing tests to set retries=0 and delay=0 so they don't wait.
type DownloadLogsOptions struct {
	MaxRetries int
	RetryDelay time.Duration
}

// DownloadLogsParams holds the parameters for the download-logs command.
type DownloadLogsParams struct {
	MigrationID     string
	GithubTargetOrg string
	TargetRepo      string
	LogFile         string
	Overwrite       bool
	MaxRetries      int
	RetryDelay      time.Duration
}

func RunDownloadLogs(ctx context.Context, gh LogDownloader, dl FileDownloader, fc FileChecker, log *logger.Logger, p DownloadLogsParams) error {
	hasMigrationID := p.MigrationID != ""
	hasOrgRepo := p.GithubTargetOrg != "" && p.TargetRepo != ""

	if !hasMigrationID && !hasOrgRepo {
		return cmdutil.NewUserError("must provide either --migration-id or both --github-target-org and --target-repo")
	}

	// Check custom filename early
	if p.LogFile != "" {
		if err := CheckFileOverwrite(fc, log, p.LogFile, p.Overwrite); err != nil {
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
		if p.GithubTargetOrg != "" || p.TargetRepo != "" {
			log.Warning("--github-target-org and --target-repo will be ignored because --migration-id was provided")
		}

		m, err := waitForMigrationLogByID(ctx, gh, p.MigrationID, p.MaxRetries, p.RetryDelay)
		if err != nil {
			return err
		}
		logURL = m.MigrationLogURL
		repoName = m.RepositoryName
		filename = fmt.Sprintf("migration-log-%s-%s.log", m.RepositoryName, p.MigrationID)
	} else {
		result, err := waitForMigrationLogByOrgRepo(ctx, gh, p.GithubTargetOrg, p.TargetRepo, p.MaxRetries, p.RetryDelay)
		if err != nil {
			return err
		}
		logURL = result.MigrationLogURL
		repoName = p.TargetRepo
		filename = fmt.Sprintf("migration-log-%s-%s-%s.log", p.GithubTargetOrg, p.TargetRepo, result.MigrationID)
	}

	if p.LogFile != "" {
		filename = p.LogFile
	} else {
		// Check default filename for overwrite
		if err := CheckFileOverwrite(fc, log, filename, p.Overwrite); err != nil {
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

func waitForMigrationLogByID(ctx context.Context, gh LogDownloader, migrationID string, maxRetries int, retryDelay time.Duration) (*github.Migration, error) {
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

func waitForMigrationLogByOrgRepo(ctx context.Context, gh LogDownloader, org, repo string, maxRetries int, retryDelay time.Duration) (*github.MigrationLogResult, error) {
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

func CheckFileOverwrite(fc FileChecker, log *logger.Logger, filepath string, overwrite bool) error {
	if !fc.FileExists(filepath) {
		return nil
	}
	if !overwrite {
		return cmdutil.NewUserErrorf("file %s already exists. Use --overwrite to overwrite it", filepath)
	}
	log.Warning("File %s already exists and will be overwritten", filepath)
	return nil
}
