package main

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"net/http"
	"testing"
	"time"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

// mockMigrateRepoGitHub implements the consumer-defined interfaces for the
// migrate-repo command (sourceGitHub + targetGitHub).
type mockMigrateRepoGitHub struct {
	// DoesRepoExist
	doesRepoExistResult bool
	doesRepoExistErr    error
	doesRepoExistOrg    string
	doesRepoExistRepo   string

	// DoesOrgExist
	doesOrgExistResult bool
	doesOrgExistErr    error

	// GetOrganizationId
	getOrgIDResult string
	getOrgIDErr    error

	// CreateGhecMigrationSource
	createMigrationSourceResult string
	createMigrationSourceErr    error

	// StartMigration
	startMigrationResult string
	startMigrationErr    error
	startMigrationCalled bool

	// GetMigration (for polling)
	getMigrationResults   []*github.Migration
	getMigrationErrors    []error
	getMigrationCallCount int

	// Archive generation
	startGitArchiveResult         int
	startGitArchiveErr            error
	startMetadataArchiveResult    int
	startMetadataArchiveErr       error
	getArchiveStatusResults       []string
	getArchiveStatusErrors        []error
	getArchiveStatusCallCount     int
	getArchiveMigrationUrlResult  string
	getArchiveMigrationUrlErr     error
	getArchiveMigrationUrlResults []string
	getArchiveMigrationUrlErrors  []error
	getArchiveMigrationUrlCount   int

	// GetVersion (for GHES version check)
	getVersionResult *github.VersionInfo
	getVersionErr    error
}

func (m *mockMigrateRepoGitHub) DoesRepoExist(_ context.Context, org, repo string) (bool, error) {
	m.doesRepoExistOrg = org
	m.doesRepoExistRepo = repo
	return m.doesRepoExistResult, m.doesRepoExistErr
}

func (m *mockMigrateRepoGitHub) DoesOrgExist(_ context.Context, _ string) (bool, error) {
	return m.doesOrgExistResult, m.doesOrgExistErr
}

func (m *mockMigrateRepoGitHub) GetOrganizationId(_ context.Context, _ string) (string, error) {
	return m.getOrgIDResult, m.getOrgIDErr
}

func (m *mockMigrateRepoGitHub) CreateGhecMigrationSource(_ context.Context, _ string) (string, error) {
	return m.createMigrationSourceResult, m.createMigrationSourceErr
}

func (m *mockMigrateRepoGitHub) StartMigration(_ context.Context, _, _, _, _, _, _ string, _ ...github.StartMigrationOption) (string, error) {
	m.startMigrationCalled = true
	return m.startMigrationResult, m.startMigrationErr
}

func (m *mockMigrateRepoGitHub) GetMigration(_ context.Context, _ string) (*github.Migration, error) {
	i := m.getMigrationCallCount
	m.getMigrationCallCount++
	if i < len(m.getMigrationResults) {
		var err error
		if i < len(m.getMigrationErrors) {
			err = m.getMigrationErrors[i]
		}
		return m.getMigrationResults[i], err
	}
	return nil, fmt.Errorf("unexpected call to GetMigration (call %d)", i)
}

func (m *mockMigrateRepoGitHub) StartGitArchiveGeneration(_ context.Context, _, _ string) (int, error) {
	return m.startGitArchiveResult, m.startGitArchiveErr
}

func (m *mockMigrateRepoGitHub) StartMetadataArchiveGeneration(_ context.Context, _, _ string, _, _ bool) (int, error) {
	return m.startMetadataArchiveResult, m.startMetadataArchiveErr
}

func (m *mockMigrateRepoGitHub) GetArchiveMigrationStatus(_ context.Context, _ string, _ int) (string, error) {
	i := m.getArchiveStatusCallCount
	m.getArchiveStatusCallCount++
	if i < len(m.getArchiveStatusResults) {
		var err error
		if i < len(m.getArchiveStatusErrors) {
			err = m.getArchiveStatusErrors[i]
		}
		return m.getArchiveStatusResults[i], err
	}
	return "", fmt.Errorf("unexpected call to GetArchiveMigrationStatus (call %d)", i)
}

func (m *mockMigrateRepoGitHub) GetArchiveMigrationUrl(_ context.Context, _ string, _ int) (string, error) {
	if len(m.getArchiveMigrationUrlResults) > 0 {
		i := m.getArchiveMigrationUrlCount
		m.getArchiveMigrationUrlCount++
		if i < len(m.getArchiveMigrationUrlResults) {
			var err error
			if i < len(m.getArchiveMigrationUrlErrors) {
				err = m.getArchiveMigrationUrlErrors[i]
			}
			return m.getArchiveMigrationUrlResults[i], err
		}
		return "", fmt.Errorf("unexpected call to GetArchiveMigrationUrl (call %d)", i)
	}
	return m.getArchiveMigrationUrlResult, m.getArchiveMigrationUrlErr
}

func (m *mockMigrateRepoGitHub) GetVersion(_ context.Context) (*github.VersionInfo, error) {
	return m.getVersionResult, m.getVersionErr
}

// mockEnvProvider implements the envProvider interface for testing.
type mockEnvProvider struct {
	sourcePAT          string
	targetPAT          string
	azureConn          string
	awsAccessKeyID     string
	awsSecretAccessKey string
	awsSessionToken    string
	awsRegion          string
}

func (m *mockEnvProvider) SourceGitHubPAT() string              { return m.sourcePAT }
func (m *mockEnvProvider) TargetGitHubPAT() string              { return m.targetPAT }
func (m *mockEnvProvider) AzureStorageConnectionString() string { return m.azureConn }
func (m *mockEnvProvider) AWSAccessKeyID() string               { return m.awsAccessKeyID }
func (m *mockEnvProvider) AWSSecretAccessKey() string           { return m.awsSecretAccessKey }
func (m *mockEnvProvider) AWSSessionToken() string              { return m.awsSessionToken }
func (m *mockEnvProvider) AWSRegion() string                    { return m.awsRegion }

// mockArchiveUploader implements the archiveUploader interface for testing.
type mockArchiveUploader struct {
	uploadResult string
	uploadErr    error
	uploadCalled bool
}

func (m *mockArchiveUploader) Upload(_ context.Context, _, _ string, _ io.ReadSeeker, _ int64) (string, error) {
	m.uploadCalled = true
	return m.uploadResult, m.uploadErr
}

// mockDownloader implements the httpDownloader interface for testing.
type mockDownloader struct {
	downloadErr    error
	downloadCalled bool
	downloadURL    string
}

func (m *mockDownloader) DownloadToFile(_ context.Context, url, _ string) error {
	m.downloadCalled = true
	m.downloadURL = url
	if m.downloadErr != nil {
		return m.downloadErr
	}
	return nil
}

// mockFileSystem implements the fileSystemProvider interface for testing.
type mockFileSystem struct {
	tempFilePath    string
	openReadContent []byte
	deleteErr       error
	deleteCalled    bool
	fileExistsVal   bool
}

func (m *mockFileSystem) GetTempFileName() string { return m.tempFilePath }
func (m *mockFileSystem) OpenRead(_ string) (io.ReadSeekCloser, int64, error) {
	r := bytes.NewReader(m.openReadContent)
	return readSeekNopCloser{r}, int64(len(m.openReadContent)), nil
}

func (m *mockFileSystem) DeleteIfExists(_ string) error {
	m.deleteCalled = true
	return m.deleteErr
}

func (m *mockFileSystem) FileExists(_ string) bool {
	return m.fileExistsVal
}

// readSeekNopCloser wraps a bytes.Reader with a no-op Close.
type readSeekNopCloser struct {
	*bytes.Reader
}

func (readSeekNopCloser) Close() error { return nil }

// ---------------------------------------------------------------------------
// Helper to build a command with all defaults
// ---------------------------------------------------------------------------

// (test helpers for creating commands are inlined in individual tests)

// ---------------------------------------------------------------------------
// Tests: Argument Validation
// ---------------------------------------------------------------------------

func TestMigrateRepo_RequiredFlags(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name:    "missing github-source-org",
			args:    []string{"--source-repo", "repo", "--github-target-org", "target-org"},
			wantErr: "--github-source-org must be provided",
		},
		{
			name:    "missing source-repo",
			args:    []string{"--github-source-org", "source-org", "--github-target-org", "target-org"},
			wantErr: "--source-repo must be provided",
		},
		{
			name:    "missing github-target-org",
			args:    []string{"--github-source-org", "source-org", "--source-repo", "repo"},
			wantErr: "--github-target-org must be provided",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_URLValidation(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name:    "github-source-org is URL",
			args:    []string{"--github-source-org", "https://github.com/my-org", "--source-repo", "repo", "--github-target-org", "target-org"},
			wantErr: "--github-source-org expects a name, not a URL",
		},
		{
			name:    "github-target-org is URL",
			args:    []string{"--github-source-org", "source-org", "--source-repo", "repo", "--github-target-org", "https://github.com/target"},
			wantErr: "--github-target-org expects a name, not a URL",
		},
		{
			name:    "source-repo is URL",
			args:    []string{"--github-source-org", "source-org", "--source-repo", "https://github.com/my-org/my-repo", "--github-target-org", "target-org"},
			wantErr: "--source-repo expects a name, not a URL",
		},
		{
			name:    "target-repo is URL",
			args:    []string{"--github-source-org", "source-org", "--source-repo", "repo", "--github-target-org", "target-org", "--target-repo", "https://github.com/org/repo"},
			wantErr: "--target-repo expects a name, not a URL",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_MutuallyExclusiveOptions(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "git-archive-url and git-archive-path",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--git-archive-url", "https://example.com/git.tar.gz",
				"--git-archive-path", "/tmp/git.tar.gz",
			},
			wantErr: "--git-archive-url and --git-archive-path may not be used together",
		},
		{
			name: "metadata-archive-url and metadata-archive-path",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--metadata-archive-url", "https://example.com/meta.tar.gz",
				"--metadata-archive-path", "/tmp/meta.tar.gz",
			},
			wantErr: "--metadata-archive-url and --metadata-archive-path may not be used together",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_PairedOptions(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "git-archive-url without metadata-archive-url",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--git-archive-url", "https://example.com/git.tar.gz",
			},
			wantErr: "you must provide both --git-archive-url --metadata-archive-url",
		},
		{
			name: "metadata-archive-url without git-archive-url",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--metadata-archive-url", "https://example.com/meta.tar.gz",
			},
			wantErr: "you must provide both --git-archive-url --metadata-archive-url",
		},
		{
			name: "git-archive-path without metadata-archive-path",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--git-archive-path", "/tmp/git.tar.gz",
			},
			wantErr: "you must provide both --git-archive-path --metadata-archive-path",
		},
		{
			name: "metadata-archive-path without git-archive-path",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--metadata-archive-path", "/tmp/meta.tar.gz",
			},
			wantErr: "you must provide both --git-archive-path --metadata-archive-path",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_GHESOnlyFlags(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "no-ssl-verify without ghes-api-url",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--no-ssl-verify",
			},
			wantErr: "--ghes-api-url must be specified when --no-ssl-verify is specified",
		},
		{
			name: "keep-archive without ghes-api-url",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--keep-archive",
			},
			wantErr: "--ghes-api-url must be specified when --keep-archive is specified",
		},
		{
			name: "aws-bucket-name without ghes-api-url and without archive paths",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--aws-bucket-name", "my-bucket",
			},
			wantErr: "you must provide --ghes-api-url",
		},
		{
			name: "use-github-storage without ghes-api-url and without archive paths",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--use-github-storage",
			},
			wantErr: "you must provide --ghes-api-url",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_StorageConflicts(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "aws-bucket-name and use-github-storage",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--ghes-api-url", "https://ghes.example.com/api/v3",
				"--aws-bucket-name", "my-bucket",
				"--use-github-storage",
			},
			wantErr: "cannot be uploaded to both",
		},
		{
			name: "azure-storage-connection-string and use-github-storage",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--ghes-api-url", "https://ghes.example.com/api/v3",
				"--azure-storage-connection-string", "DefaultEndpointsProtocol=https;...",
				"--use-github-storage",
			},
			wantErr: "cannot be uploaded to both",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_TargetRepoVisibility(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "invalid visibility",
			args: []string{
				"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
				"--target-repo-visibility", "secret",
			},
			wantErr: "--target-repo-visibility must be one of",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockMigrateRepoGitHub{}

			cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateRepo_DefaultsTargetRepoToSourceRepo(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationResult:        "RM_123",
		getMigrationResults: []*github.Migration{
			{State: "SUCCEEDED", RepositoryName: "my-repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src-org",
		"--source-repo", "my-repo",
		"--github-target-org", "tgt-org",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "my-repo")
}

func TestMigrateRepo_SourcePATDefaultsToTargetPAT(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(true, &buf, &buf) // verbose

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationResult:        "RM_123",
		getMigrationResults: []*github.Migration{
			{State: "SUCCEEDED", RepositoryName: "repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src-org",
		"--source-repo", "repo",
		"--github-target-org", "tgt-org",
		"--github-target-pat", "target-token",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "github-source-pat will also use its value")
}

// ---------------------------------------------------------------------------
// Tests: Happy path — GitHub.com to GitHub.com
// ---------------------------------------------------------------------------

func TestMigrateRepo_HappyPath_GithubToGithub(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID_123",
		createMigrationSourceResult: "MS_456",
		startMigrationResult:        "RM_789",
		getMigrationResults: []*github.Migration{
			{State: "SUCCEEDED", RepositoryName: "repo", WarningsCount: 0, MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "source-org",
		"--source-repo", "repo",
		"--github-target-org", "target-org",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Migrating Repo")
	assert.Contains(t, output, "SUCCEEDED")
	assert.Contains(t, output, "Migration log available at")
	assert.True(t, gh.startMigrationCalled)
}

func TestMigrateRepo_HappyPath_QueueOnly(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationResult:        "RM_999",
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "successfully queued")
	assert.Contains(t, output, "RM_999")
	// GetMigration should NOT have been called
	assert.Equal(t, 0, gh.getMigrationCallCount)
}

func TestMigrateRepo_MigrationFails(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationResult:        "RM_FAIL",
		getMigrationResults: []*github.Migration{
			{State: "FAILED", RepositoryName: "repo", FailureReason: "something broke", WarningsCount: 3, MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "something broke")

	output := buf.String()
	assert.Contains(t, output, "Migration Failed")
	assert.Contains(t, output, "3 warnings")
}

func TestMigrateRepo_AlreadyExists(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationErr:           fmt.Errorf("A repository called tgt/repo already exists"),
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
	})

	err := cmd.Execute()
	require.NoError(t, err) // should NOT error

	output := buf.String()
	assert.Contains(t, output, "already contains a repository")
}

func TestMigrateRepo_PermissionsError(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:           "ORG_ID",
		createMigrationSourceErr: fmt.Errorf("not have the correct permissions to execute"),
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "not have the correct permissions")
	assert.Contains(t, err.Error(), "you are a member of the")
}

func TestMigrateRepo_MigrationPolls(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationResult:        "RM_POLL",
		getMigrationResults: []*github.Migration{
			{State: "IN_PROGRESS", RepositoryName: "repo"},
			{State: "IN_PROGRESS", RepositoryName: "repo"},
			{State: "SUCCEEDED", RepositoryName: "repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, 3, gh.getMigrationCallCount)
}

// ---------------------------------------------------------------------------
// Tests: GHES migration with archive generation
// ---------------------------------------------------------------------------

func TestMigrateRepo_GHES_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		// GHES version check — version < 3.8 means blob creds required
		getVersionResult: &github.VersionInfo{Version: "3.7.0"},

		// Target checks
		doesRepoExistResult: false,
		doesOrgExistResult:  true,

		// Migration setup
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",

		// Archive generation
		startGitArchiveResult:      100,
		startMetadataArchiveResult: 200,
		getArchiveStatusResults:    []string{"exported", "exported"},
		getArchiveMigrationUrlResults: []string{
			"https://ghes.example.com/archive/git.tar.gz",
			"https://ghes.example.com/archive/meta.tar.gz",
		},

		// Migration
		startMigrationResult: "RM_GHES",
		getMigrationResults: []*github.Migration{
			{State: "SUCCEEDED", RepositoryName: "repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	uploader := &mockArchiveUploader{uploadResult: "https://blob.example.com/archive"}
	downloader := &mockDownloader{}
	fs := &mockFileSystem{
		tempFilePath:    "/tmp/test-archive",
		openReadContent: []byte("archive-data"),
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, uploader, downloader, fs, log, migrateRepoOptions{
		pollInterval:        0,
		archivePollInterval: 0,
		archiveTimeout:      1 * time.Second,
	})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src-org",
		"--source-repo", "repo",
		"--github-target-org", "tgt-org",
		"--ghes-api-url", "https://ghes.example.com/api/v3",
		"--azure-storage-connection-string", "DefaultEndpointsProtocol=https;...",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.True(t, downloader.downloadCalled, "should have downloaded archives")
	assert.True(t, uploader.uploadCalled, "should have uploaded archives")

	output := buf.String()
	assert.Contains(t, output, "SUCCEEDED")
}

func TestMigrateRepo_GHES_TargetRepoAlreadyExists(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getVersionResult:    &github.VersionInfo{Version: "3.7.0"},
		doesRepoExistResult: true,
		doesOrgExistResult:  true,
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
		"--ghes-api-url", "https://ghes.example.com/api/v3",
		"--azure-storage-connection-string", "conn-string",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "already exists")
}

func TestMigrateRepo_GHES_TargetOrgDoesNotExist(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getVersionResult:    &github.VersionInfo{Version: "3.7.0"},
		doesRepoExistResult: false,
		doesOrgExistResult:  false,
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, &mockArchiveUploader{}, &mockDownloader{}, &mockFileSystem{}, log, migrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
		"--ghes-api-url", "https://ghes.example.com/api/v3",
		"--azure-storage-connection-string", "conn-string",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "does not exist")
}

// ---------------------------------------------------------------------------
// Tests: Local archive path upload
// ---------------------------------------------------------------------------

func TestMigrateRepo_LocalArchivePaths(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockMigrateRepoGitHub{
		getOrgIDResult:              "ORG_ID",
		createMigrationSourceResult: "MS_ID",
		startMigrationResult:        "RM_LOCAL",
		getMigrationResults: []*github.Migration{
			{State: "SUCCEEDED", RepositoryName: "repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	uploader := &mockArchiveUploader{uploadResult: "https://blob.example.com/archive"}
	fs := &mockFileSystem{
		openReadContent: []byte("archive-data"),
		fileExistsVal:   true,
	}

	cmd := newMigrateRepoCmd(gh, gh, &mockEnvProvider{targetPAT: "token"}, uploader, &mockDownloader{}, fs, log, migrateRepoOptions{pollInterval: 0})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-source-org", "src", "--source-repo", "repo", "--github-target-org", "tgt",
		"--git-archive-path", "/tmp/git.tar.gz",
		"--metadata-archive-path", "/tmp/meta.tar.gz",
		"--use-github-storage",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.True(t, uploader.uploadCalled)
	output := buf.String()
	assert.Contains(t, output, "SUCCEEDED")
}

// Prevent import of "net/http" from being unused - used in mock types
var _ = http.StatusOK
