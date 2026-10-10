package main

import (
	"bytes"
	"context"
	"fmt"
	"testing"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockLogDownloader implements the logDownloader interface for testing.
type mockLogDownloader struct {
	getMigrationResult  *github.Migration
	getMigrationResults []*github.Migration // if set, returns results[calls-1] per call
	getMigrationErr     error
	getMigrationCalls   int

	getMigrationLogURLResult  *github.MigrationLogResult
	getMigrationLogURLResults []*github.MigrationLogResult // if set, returns results[calls-1] per call
	getMigrationLogURLErr     error
	getMigrationLogURLCalls   int
}

func (m *mockLogDownloader) GetMigration(_ context.Context, _ string) (*github.Migration, error) {
	m.getMigrationCalls++
	if m.getMigrationResults != nil && m.getMigrationCalls <= len(m.getMigrationResults) {
		return m.getMigrationResults[m.getMigrationCalls-1], m.getMigrationErr
	}
	return m.getMigrationResult, m.getMigrationErr
}

func (m *mockLogDownloader) GetMigrationLogUrl(_ context.Context, _, _ string) (*github.MigrationLogResult, error) {
	m.getMigrationLogURLCalls++
	if m.getMigrationLogURLResults != nil && m.getMigrationLogURLCalls <= len(m.getMigrationLogURLResults) {
		return m.getMigrationLogURLResults[m.getMigrationLogURLCalls-1], m.getMigrationLogURLErr
	}
	return m.getMigrationLogURLResult, m.getMigrationLogURLErr
}

// mockFileDownloader implements the fileDownloader interface for testing.
type mockFileDownloader struct {
	err     error
	calls   int
	gotURL  string
	gotPath string
}

func (m *mockFileDownloader) DownloadToFile(_ context.Context, url, filepath string) error {
	m.calls++
	m.gotURL = url
	m.gotPath = filepath
	return m.err
}

// mockFileChecker implements the fileChecker interface for testing.
type mockFileChecker struct {
	exists bool
}

func (m *mockFileChecker) FileExists(_ string) bool {
	return m.exists
}

func TestDownloadLogs_ByMigrationID_Success(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_123",
			RepositoryName:  "my-repo",
			MigrationLogURL: "https://example.com/log",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123"})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, 1, gh.getMigrationCalls)
	assert.Equal(t, 1, dl.calls)
	assert.Equal(t, "https://example.com/log", dl.gotURL)
	assert.Contains(t, dl.gotPath, "migration-log-my-repo-RM_123.log")

	output := buf.String()
	assert.Contains(t, output, "Downloading migration logs")
	assert.Contains(t, output, "Downloading log for repository my-repo to migration-log-my-repo-RM_123.log")
	assert.Contains(t, output, "Downloaded my-repo log to migration-log-my-repo-RM_123.log")
}

func TestDownloadLogs_ByOrgRepo_Success(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationLogURLResult: &github.MigrationLogResult{
			MigrationLogURL: "https://example.com/org-log",
			MigrationID:     "RM_456",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--github-target-org", "my-org", "--target-repo", "my-repo"})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, 1, gh.getMigrationLogURLCalls)
	assert.Equal(t, 1, dl.calls)
	assert.Equal(t, "https://example.com/org-log", dl.gotURL)
	assert.Contains(t, dl.gotPath, "migration-log-my-org-my-repo-RM_456.log")
}

func TestDownloadLogs_ByMigrationID_LogURLEmpty_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_789",
			RepositoryName:  "my-repo",
			MigrationLogURL: "", // empty — retry exhausted
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_789"})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "migration log URL")
	assert.Equal(t, 0, dl.calls)
}

func TestDownloadLogs_ByOrgRepo_MigrationNotFound_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationLogURLResult: nil, // no migration found
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--github-target-org", "my-org", "--target-repo", "my-repo"})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "no migration found")
	assert.Equal(t, 0, dl.calls)
}

func TestDownloadLogs_FileExists_NoOverwrite_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_123",
			RepositoryName:  "my-repo",
			MigrationLogURL: "https://example.com/log",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: true}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123"})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "already exists")
	assert.Equal(t, 0, dl.calls)
}

func TestDownloadLogs_FileExists_WithOverwrite_Success(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_123",
			RepositoryName:  "my-repo",
			MigrationLogURL: "https://example.com/log",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: true}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123", "--overwrite"})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, 1, dl.calls)

	output := buf.String()
	assert.Contains(t, output, "already exists")
}

func TestDownloadLogs_NeitherMigrationIDNorOrgRepo_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "must provide either --migration-id or both --github-target-org and --target-repo")
}

func TestDownloadLogs_MigrationIDWithOrgRepo_WarnsAndUsesMigrationID(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_123",
			RepositoryName:  "my-repo",
			MigrationLogURL: "https://example.com/log",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123", "--github-target-org", "my-org", "--target-repo", "my-repo"})

	err := cmd.Execute()
	require.NoError(t, err)

	// Should use migration ID path, not org/repo
	assert.Equal(t, 1, gh.getMigrationCalls)
	assert.Equal(t, 0, gh.getMigrationLogURLCalls)

	output := buf.String()
	assert.Contains(t, output, "will be ignored")
}

func TestDownloadLogs_CustomFilename(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_123",
			RepositoryName:  "my-repo",
			MigrationLogURL: "https://example.com/log",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123", "--migration-log-file", "custom.log"})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, "custom.log", dl.gotPath)
}

func TestDownloadLogs_ByOrgRepo_LogURLEmpty_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationLogURLResult: &github.MigrationLogResult{
			MigrationLogURL: "", // empty — retry exhausted
			MigrationID:     "RM_456",
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--github-target-org", "my-org", "--target-repo", "my-repo"})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "migration log URL")
	assert.Equal(t, 0, dl.calls)
}

func TestDownloadLogs_DownloadError_PropagatesError(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResult: &github.Migration{
			ID:              "RM_123",
			RepositoryName:  "my-repo",
			MigrationLogURL: "https://example.com/log",
		},
	}
	dl := &mockFileDownloader{err: fmt.Errorf("download failed: network error")}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123"})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "network error")
}

func TestDownloadLogs_ByMigrationID_RetrySucceedsOnSecondAttempt(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{
		getMigrationResults: []*github.Migration{
			{ID: "RM_123", RepositoryName: "my-repo", MigrationLogURL: ""},                              // first call: empty
			{ID: "RM_123", RepositoryName: "my-repo", MigrationLogURL: "https://example.com/log-retry"}, // second call: populated
		},
	}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 1, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--migration-id", "RM_123"})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, 2, gh.getMigrationCalls)
	assert.Equal(t, 1, dl.calls)
	assert.Equal(t, "https://example.com/log-retry", dl.gotURL)
}

func TestDownloadLogs_PartialOrgRepo_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockLogDownloader{}
	dl := &mockFileDownloader{}
	fc := &mockFileChecker{exists: false}

	// Only --github-target-org, missing --target-repo
	cmd := newDownloadLogsCmd(gh, dl, fc, log, downloadLogsOptions{MaxRetries: 0, RetryDelay: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--github-target-org", "my-org"})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "must provide either --migration-id or both --github-target-org and --target-repo")
}
