package main

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"testing"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Test constants — matching C# MigrateRepoCommandHandlerTests
// ---------------------------------------------------------------------------

const (
	bbsGithubOrg       = "target-org"
	bbsGithubRepo      = "target-repo"
	bbsGithubOrgID     = "github-org-id"
	bbsMigSourceID     = "migration-source-id"
	bbsMigrationID     = "migration-id"
	bbsGithubPAT       = "github-pat"
	bbsServerURL       = "https://our-bbs-server.com"
	bbsHost            = "our-bbs-server.com"
	bbsUsername        = "bbs-username"
	bbsPassword        = "bbs-password"
	bbsProject         = "bbs-project"
	bbsRepo            = "bbs-repo"
	bbsRepoURL         = bbsServerURL + "/projects/" + bbsProject + "/repos/" + bbsRepo + "/browse"
	bbsUnusedRepoURL   = "https://not-used"
	bbsArchivePath     = "path/to/archive.tar"
	bbsArchiveURL      = "https://archive-url/bbs-archive.tar"
	bbsSSHUser         = "ssh-user"
	bbsSSHPrivateKey   = "private-key"
	bbsSMBUser         = "smb-user"
	bbsSMBPassword     = "smb-password"
	bbsAzureConnStr    = "azure-storage-connection-string"
	bbsAWSBucket       = "aws-bucket-name"
	bbsAWSAccessKey    = "aws-access-key-id"
	bbsAWSSecretKey    = "aws-secret-access-key"
	bbsAWSSessionToken = "aws-session-token"
	bbsAWSRegion       = "eu-west-1"
)

const bbsExportID int64 = 123

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

// mockBbsGitHub implements bbsMigrateRepoGitHub.
type mockBbsGitHub struct {
	doesRepoExistResult bool
	doesRepoExistErr    error

	getOrgIDResult string
	getOrgIDErr    error

	createMigrationSourceResult string
	createMigrationSourceErr    error

	startBbsMigrationResult string
	startBbsMigrationErr    error
	startBbsMigrationCalled bool

	// Capture args for verification
	startBbsMigrationArgs struct {
		migrationSourceID    string
		bbsRepoURL           string
		orgID                string
		repo                 string
		targetToken          string
		archiveURL           string
		targetRepoVisibility string
	}

	getMigrationResults   []*github.Migration
	getMigrationErrors    []error
	getMigrationCallCount int
}

func (m *mockBbsGitHub) DoesRepoExist(_ context.Context, _, _ string) (bool, error) {
	return m.doesRepoExistResult, m.doesRepoExistErr
}

func (m *mockBbsGitHub) GetOrganizationId(_ context.Context, _ string) (string, error) {
	return m.getOrgIDResult, m.getOrgIDErr
}

func (m *mockBbsGitHub) CreateBbsMigrationSource(_ context.Context, _ string) (string, error) {
	return m.createMigrationSourceResult, m.createMigrationSourceErr
}

func (m *mockBbsGitHub) StartBbsMigration(_ context.Context, migrationSourceID, bbsRepoURL, orgID, repo, targetToken, archiveURL, targetRepoVisibility string) (string, error) {
	m.startBbsMigrationCalled = true
	m.startBbsMigrationArgs.migrationSourceID = migrationSourceID
	m.startBbsMigrationArgs.bbsRepoURL = bbsRepoURL
	m.startBbsMigrationArgs.orgID = orgID
	m.startBbsMigrationArgs.repo = repo
	m.startBbsMigrationArgs.targetToken = targetToken
	m.startBbsMigrationArgs.archiveURL = archiveURL
	m.startBbsMigrationArgs.targetRepoVisibility = targetRepoVisibility
	return m.startBbsMigrationResult, m.startBbsMigrationErr
}

func (m *mockBbsGitHub) GetMigration(_ context.Context, _ string) (*github.Migration, error) {
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

// mockBbsAPI implements bbsMigrateRepoBbsAPI.
type mockBbsAPI struct {
	startExportResult int64
	startExportErr    error
	startExportCalled bool

	// Sequence of GetExport results
	getExportStates []struct {
		state      string
		message    string
		percentage int
		err        error
	}
	getExportCallCount int
}

func (m *mockBbsAPI) StartExport(_ context.Context, _, _ string) (int64, error) {
	m.startExportCalled = true
	return m.startExportResult, m.startExportErr
}

func (m *mockBbsAPI) GetExport(_ context.Context, _ int64) (string, string, int, error) {
	i := m.getExportCallCount
	m.getExportCallCount++
	if i < len(m.getExportStates) {
		s := m.getExportStates[i]
		return s.state, s.message, s.percentage, s.err
	}
	return "", "", 0, fmt.Errorf("unexpected call to GetExport (call %d)", i)
}

// mockBbsDownloader implements bbsMigrateRepoArchiveDownloader.
type mockBbsDownloader struct {
	downloadResult string
	downloadErr    error
	downloadCalled bool
}

func (m *mockBbsDownloader) Download(_ int64, _ string) (string, error) {
	m.downloadCalled = true
	return m.downloadResult, m.downloadErr
}

// mockBbsUploader implements bbsMigrateRepoArchiveUploader.
type mockBbsUploader struct {
	uploadResult string
	uploadErr    error
	uploadCalled bool
}

func (m *mockBbsUploader) Upload(_ context.Context, _, _ string, _ io.ReadSeeker, _ int64) (string, error) {
	m.uploadCalled = true
	return m.uploadResult, m.uploadErr
}

// mockBbsFileSystem implements bbsMigrateRepoFileSystem.
type mockBbsFileSystem struct {
	fileExistsVal    bool
	dirExistsVal     bool
	openReadContent  []byte
	openReadErr      error
	deleteErr        error
	deleteCalled     bool
	deleteCalledPath string
	openReadCalled   bool
	openReadPath     string
}

func (m *mockBbsFileSystem) FileExists(_ string) bool      { return m.fileExistsVal }
func (m *mockBbsFileSystem) DirectoryExists(_ string) bool { return m.dirExistsVal }
func (m *mockBbsFileSystem) OpenRead(path string) (io.ReadSeekCloser, int64, error) {
	m.openReadCalled = true
	m.openReadPath = path
	if m.openReadErr != nil {
		return nil, 0, m.openReadErr
	}
	r := bytes.NewReader(m.openReadContent)
	return bbsReadSeekNopCloser{r}, int64(len(m.openReadContent)), nil
}

func (m *mockBbsFileSystem) DeleteIfExists(path string) error {
	m.deleteCalled = true
	m.deleteCalledPath = path
	return m.deleteErr
}

type bbsReadSeekNopCloser struct{ *bytes.Reader }

func (bbsReadSeekNopCloser) Close() error { return nil }

// mockBbsEnvProvider implements bbsMigrateRepoEnvProvider.
type mockBbsEnvProvider struct {
	targetPAT  string
	azureConn  string
	awsAccess  string
	awsSecret  string
	awsSession string
	awsRegion  string
	bbsUser    string
	bbsPass    string
	smbPass    string
}

func (m *mockBbsEnvProvider) TargetGitHubPAT() string              { return m.targetPAT }
func (m *mockBbsEnvProvider) AzureStorageConnectionString() string { return m.azureConn }
func (m *mockBbsEnvProvider) AWSAccessKeyID() string               { return m.awsAccess }
func (m *mockBbsEnvProvider) AWSSecretAccessKey() string           { return m.awsSecret }
func (m *mockBbsEnvProvider) AWSSessionToken() string              { return m.awsSession }
func (m *mockBbsEnvProvider) AWSRegion() string                    { return m.awsRegion }
func (m *mockBbsEnvProvider) BBSUsername() string                  { return m.bbsUser }
func (m *mockBbsEnvProvider) BBSPassword() string                  { return m.bbsPass }
func (m *mockBbsEnvProvider) SmbPassword() string                  { return m.smbPass }

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func completedExport() []struct {
	state      string
	message    string
	percentage int
	err        error
} {
	return []struct {
		state      string
		message    string
		percentage int
		err        error
	}{
		{"COMPLETED", "The export is complete", 100, nil},
	}
}

func defaultBbsOpts() bbsMigrateRepoOptions {
	return bbsMigrateRepoOptions{
		pollInterval:       0,
		exportPollInterval: 0,
	}
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — Generate Only
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_GenerateOnly(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	gh := &mockBbsGitHub{}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, bbsAPI.startExportCalled)
	// DoesRepoExist should NOT be called (no import phase)
	assert.False(t, gh.startBbsMigrationCalled)
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — Generate And Download
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_GenerateAndDownload(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, downloader, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, downloader.downloadCalled)
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — Ingest Only (archive-url provided)
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_IngestOnly(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	envProv := &mockBbsEnvProvider{targetPAT: bbsGithubPAT}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	assert.Equal(t, bbsMigSourceID, gh.startBbsMigrationArgs.migrationSourceID)
	assert.Equal(t, bbsUnusedRepoURL, gh.startBbsMigrationArgs.bbsRepoURL)
	assert.Equal(t, bbsGithubOrgID, gh.startBbsMigrationArgs.orgID)
	assert.Equal(t, bbsGithubRepo, gh.startBbsMigrationArgs.repo)
	assert.Equal(t, bbsGithubPAT, gh.startBbsMigrationArgs.targetToken)
	assert.Equal(t, bbsArchiveURL, gh.startBbsMigrationArgs.archiveURL)
	assert.Empty(t, gh.startBbsMigrationArgs.targetRepoVisibility)
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — Full Flow SSH + Azure Upload + Ingest
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_SshAzureUploadAndIngest(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, downloader, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	assert.Equal(t, bbsMigSourceID, gh.startBbsMigrationArgs.migrationSourceID)
	assert.Equal(t, bbsRepoURL, gh.startBbsMigrationArgs.bbsRepoURL)
	assert.Equal(t, bbsGithubOrgID, gh.startBbsMigrationArgs.orgID)
	assert.Equal(t, bbsGithubRepo, gh.startBbsMigrationArgs.repo)
	assert.Equal(t, bbsGithubPAT, gh.startBbsMigrationArgs.targetToken)
	assert.Equal(t, bbsArchiveURL, gh.startBbsMigrationArgs.archiveURL)
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — Full Flow SSH + AWS Upload + Ingest
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_SshAwsUploadAndIngest(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, downloader, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--aws-bucket-name", bbsAWSBucket,
		"--aws-access-key", bbsAWSAccessKey,
		"--aws-secret-key", bbsAWSSecretKey,
		"--aws-region", bbsAWSRegion,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	assert.Equal(t, bbsMigSourceID, gh.startBbsMigrationArgs.migrationSourceID)
	assert.Equal(t, bbsRepoURL, gh.startBbsMigrationArgs.bbsRepoURL)
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — Running On BBS Server (no SSH/SMB, local filesystem)
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_RunningOnBbsServer(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, &mockBbsDownloader{}, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--bbs-shared-home", "bbs-shared-home",
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	// Archive path should be set to shared home path
	assert.True(t, fs.openReadCalled)
	assert.Contains(t, fs.openReadPath, "bbs-shared-home")
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — BBS Credentials Via Environment
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_BbsCredentialsViaEnv(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	envProv := &mockBbsEnvProvider{
		bbsUser: bbsUsername,
		bbsPass: bbsPassword,
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, downloader, uploader, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	assert.Equal(t, bbsRepoURL, gh.startBbsMigrationArgs.bbsRepoURL)
}

// ---------------------------------------------------------------------------
// Tests: Happy Path — GitHub Storage
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_HappyPath_GithubStorage(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	uploader := &mockBbsUploader{uploadResult: "gei://archive/1"}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive-data")}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--use-github-storage",
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	assert.Equal(t, "gei://archive/1", gh.startBbsMigrationArgs.archiveURL)
}

// ---------------------------------------------------------------------------
// Tests: Archive Deletion
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_DeletesDownloadedArchive(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}, bbsAPI, downloader, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, fs.deleteCalled)
	assert.Equal(t, bbsArchivePath, fs.deleteCalledPath)
}

func TestBbsMigrateRepo_DeletesDownloadedArchiveEvenIfUploadFails(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadErr: fmt.Errorf("upload failed")}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
	}, bbsAPI, downloader, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
	})

	err := cmd.Execute()
	require.Error(t, err)
	// Archive should still be deleted even though upload failed
	assert.True(t, fs.deleteCalled)
}

func TestBbsMigrateRepo_DoesNotThrowIfFailsToDeleteArchive(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{
		fileExistsVal:   true,
		dirExistsVal:    true,
		openReadContent: []byte("archive"),
		deleteErr:       fmt.Errorf("access denied"),
	}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}, bbsAPI, downloader, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err) // Should NOT fail
	assert.True(t, fs.deleteCalled)
	// Warning should be logged
	assert.Contains(t, buf.String(), "Couldn't delete")
}

// ---------------------------------------------------------------------------
// Tests: Target Repo Exists
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_DontGenerateIfTargetRepoExists(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         true,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
	}
	bbsAPI := &mockBbsAPI{}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", bbsGithubPAT,
		"--queue-only",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "already exists")
	assert.False(t, bbsAPI.startExportCalled)
}

// ---------------------------------------------------------------------------
// Tests: GitHub PAT usage
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_UsesGitHubPatWhenProvided(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	specificPat := "specific-github-pat"
	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--github-pat", specificPat,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, specificPat, gh.startBbsMigrationArgs.targetToken)
}

// ---------------------------------------------------------------------------
// Tests: Skip Migration If Exists (during StartBbsMigration)
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_SkipMigrationIfRepoExistsDuringStart(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationErr:        fmt.Errorf("A repository called %s/%s already exists", bbsGithubOrg, bbsGithubRepo),
	}
	envProv := &mockBbsEnvProvider{targetPAT: bbsGithubPAT}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err) // Should NOT error
	assert.Contains(t, buf.String(), "already contains a repository")
}

// ---------------------------------------------------------------------------
// Tests: Permissions Error
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsDecoratedPermissionsError(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:      false,
		getOrgIDResult:           bbsGithubOrgID,
		createMigrationSourceErr: fmt.Errorf("monalisa does not have the correct permissions to execute `CreateMigrationSource`"),
	}
	envProv := &mockBbsEnvProvider{targetPAT: bbsGithubPAT}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "not have the correct permissions")
	assert.Contains(t, err.Error(), "you are a member of the")
}

// ---------------------------------------------------------------------------
// Tests: Export Fails
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsIfExportFails(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates: []struct {
			state      string
			message    string
			percentage int
			err        error
		}{
			{"FAILED", "The export failed", 0, nil},
		},
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "BBS export failed")
}

func TestBbsMigrateRepo_ThrowsIfExportAborted(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates: []struct {
			state      string
			message    string
			percentage int
			err        error
		}{
			{"ABORTED", "The export was aborted", 0, nil},
		},
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "BBS export failed")
}

func TestBbsMigrateRepo_RunningStateIsInProgress(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
		getMigrationResults: []*github.Migration{
			{State: "SUCCEEDED", MigrationLogURL: "https://example.com/log"},
		},
	}
	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates: []struct {
			state      string
			message    string
			percentage int
			err        error
		}{
			{"RUNNING", "Export is running", 50, nil},
			{"COMPLETED", "The export is complete", 100, nil},
		},
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	envProv := &mockBbsEnvProvider{targetPAT: bbsGithubPAT}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive-data")}

	cmd := newBbsMigrateRepoCmd(gh, bbsAPI, downloader, uploader, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--azure-storage-connection-string", bbsAzureConnStr,
	})

	err := cmd.Execute()
	require.NoError(t, err)
	// RUNNING should have been polled through (2 GetExport calls: RUNNING then COMPLETED)
	assert.Equal(t, 2, bbsAPI.getExportCallCount)
}

// ---------------------------------------------------------------------------
// Tests: Archive Path Usage
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_UsesArchivePathIfProvided(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	envProv := &mockBbsEnvProvider{targetPAT: bbsGithubPAT}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive-data")}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, uploader, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, gh.startBbsMigrationCalled)
	assert.Equal(t, bbsUnusedRepoURL, gh.startBbsMigrationArgs.bbsRepoURL)
}

func TestBbsMigrateRepo_ArchivePathIsPreserved(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("data")}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	// OpenRead should be called with the archive path
	assert.True(t, fs.openReadCalled)
	assert.Equal(t, bbsArchivePath, fs.openReadPath)
}

// ---------------------------------------------------------------------------
// Tests: Archive URL Skips Upload
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ArchiveUrlSkipsUpload(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	uploader := &mockBbsUploader{}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, uploader, fs, &mockBbsEnvProvider{targetPAT: bbsGithubPAT}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.False(t, uploader.uploadCalled)
}

// ---------------------------------------------------------------------------
// Tests: SMB Password Validation
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsWhenSmbUserWithoutSmbPassword(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--smb-user", bbsSMBUser,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "must be specified for SMB download")
}

func TestBbsMigrateRepo_SmbPasswordViaEnvironment(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	envProv := &mockBbsEnvProvider{smbPass: bbsSMBPassword}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, &mockBbsDownloader{}, &mockBbsUploader{}, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
	})

	err := cmd.Execute()
	require.NoError(t, err)
}

// ---------------------------------------------------------------------------
// Tests: AWS Upload
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_UsesAwsIfCredentialsPassed(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	envProv := &mockBbsEnvProvider{targetPAT: bbsGithubPAT}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("archive")}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, uploader, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--archive-path", bbsArchivePath,
		"--aws-access-key", bbsAWSAccessKey,
		"--aws-secret-key", bbsAWSSecretKey,
		"--aws-bucket-name", bbsAWSBucket,
		"--aws-region", bbsAWSRegion,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, uploader.uploadCalled)
	assert.True(t, gh.startBbsMigrationCalled)
}

// ---------------------------------------------------------------------------
// Tests: Storage Validation Errors
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsWhenNoStorageProvided(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--azure-storage-connection-string")
	assert.Contains(t, err.Error(), "--aws-bucket-name")
	assert.Contains(t, err.Error(), "--use-github-storage")
}

func TestBbsMigrateRepo_ThrowsWhenBothAzureAndAwsProvided(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--aws-bucket-name", bbsAWSBucket,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "cannot be specified together")
}

func TestBbsMigrateRepo_ThrowsWhenAwsBucketWithoutAccessKey(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--aws-bucket-name", bbsAWSBucket,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--aws-access-key")
	assert.Contains(t, err.Error(), "AWS_ACCESS_KEY_ID")
}

func TestBbsMigrateRepo_ThrowsWhenAwsBucketWithoutSecretKey(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--aws-bucket-name", bbsAWSBucket,
		"--aws-access-key", bbsAWSAccessKey,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--aws-secret-key")
	assert.Contains(t, err.Error(), "AWS_SECRET_ACCESS_KEY")
}

func TestBbsMigrateRepo_ThrowsWhenAwsBucketWithoutRegion(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--aws-bucket-name", bbsAWSBucket,
		"--aws-access-key", bbsAWSAccessKey,
		"--aws-secret-key", bbsAWSSecretKey,
		"--aws-session-token", bbsAWSSessionToken,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--aws-region")
	assert.Contains(t, err.Error(), "AWS_REGION")
}

// ---------------------------------------------------------------------------
// Tests: BBS Credential Validation
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ErrorsWhenNoBbsUsername(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "BBS_USERNAME")
	assert.Contains(t, err.Error(), "--bbs-username")
}

func TestBbsMigrateRepo_ErrorsWhenNoBbsPassword(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--bbs-username", bbsUsername,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "BBS_PASSWORD")
	assert.Contains(t, err.Error(), "--bbs-password")
}

// ---------------------------------------------------------------------------
// Tests: Kerberos Bypasses Username/Password Validation
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_KerberosSkipsCredentialValidation(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--kerberos",
	})

	err := cmd.Execute()
	require.NoError(t, err)
}

// ---------------------------------------------------------------------------
// Tests: Target Repo Visibility
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_SetsTargetRepoVisibility(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{targetPAT: bbsGithubPAT}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
		"--target-repo-visibility", "public",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, "public", gh.startBbsMigrationArgs.targetRepoVisibility)
}

// ---------------------------------------------------------------------------
// Tests: Archive Path Does Not Exist
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsWhenArchivePathDoesNotExist(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: false, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", "/path/to/nonexistent/archive.tar",
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--azure-storage-connection-string", bbsAzureConnStr,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "/path/to/nonexistent/archive.tar")
}

// ---------------------------------------------------------------------------
// Tests: BBS Shared Home Validation
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsWhenBbsSharedHomeDoesNotExist(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: false}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--bbs-shared-home", "/nonexistent/shared/home",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "/nonexistent/shared/home")
}

// ---------------------------------------------------------------------------
// Tests: SSH/SMB Bypass Shared Home Validation
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_SshBypassesSharedHomeValidation(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: false}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, downloader, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--bbs-shared-home", "/nonexistent/shared/home",
		"--ssh-user", bbsSSHUser,
		"--ssh-private-key", bbsSSHPrivateKey,
	})

	err := cmd.Execute()
	require.NoError(t, err)
}

func TestBbsMigrateRepo_SmbBypassesSharedHomeValidation(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	bbsAPI := &mockBbsAPI{
		startExportResult: bbsExportID,
		getExportStates:   completedExport(),
	}
	downloader := &mockBbsDownloader{downloadResult: bbsArchivePath}
	envProv := &mockBbsEnvProvider{smbPass: bbsSMBPassword}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: false}

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, bbsAPI, downloader, &mockBbsUploader{}, fs, envProv, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--bbs-shared-home", "/nonexistent/shared/home",
		"--smb-user", bbsSMBUser,
	})

	err := cmd.Execute()
	require.NoError(t, err)
}

// ---------------------------------------------------------------------------
// Tests: Archive Path Logging
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_LogsArchivePathBeforeUpload(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	uploader := &mockBbsUploader{uploadResult: bbsArchiveURL}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true, openReadContent: []byte("data")}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, uploader, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", bbsArchivePath,
		"--azure-storage-connection-string", bbsAzureConnStr,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Contains(t, buf.String(), bbsArchivePath)
}

// ---------------------------------------------------------------------------
// Tests: Migration Polling
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_MigrationFailsWithReason(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
		getMigrationResults: []*github.Migration{
			{State: "FAILED", FailureReason: "something broke", WarningsCount: 3, MigrationLogURL: "https://example.com/log"},
		},
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{targetPAT: bbsGithubPAT}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "something broke")
	assert.Contains(t, buf.String(), "Migration Failed")
	assert.Contains(t, buf.String(), "3 warnings")
}

func TestBbsMigrateRepo_MigrationSucceeds(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
		getMigrationResults: []*github.Migration{
			{State: "IN_PROGRESS"},
			{State: "SUCCEEDED", MigrationLogURL: "https://example.com/log"},
		},
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{targetPAT: bbsGithubPAT}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, 2, gh.getMigrationCallCount)
	assert.Contains(t, buf.String(), "SUCCEEDED")
}

func TestBbsMigrateRepo_QueueOnlySkipsPolling(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockBbsGitHub{
		doesRepoExistResult:         false,
		getOrgIDResult:              bbsGithubOrgID,
		createMigrationSourceResult: bbsMigSourceID,
		startBbsMigrationResult:     bbsMigrationID,
	}
	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}

	cmd := newBbsMigrateRepoCmd(gh, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{targetPAT: bbsGithubPAT}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-url", bbsArchiveURL,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, 0, gh.getMigrationCallCount)
	assert.Contains(t, buf.String(), "successfully queued")
}

// ---------------------------------------------------------------------------
// Tests: Source Validation
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsWhenNoSourceSpecified(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, &mockBbsFileSystem{}, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "Either --bbs-server-url, --archive-path, or --archive-url must be specified")
}

func TestBbsMigrateRepo_ThrowsWhenBothArchivePathAndArchiveUrl(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, &mockBbsFileSystem{fileExistsVal: true}, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--archive-path", "/tmp/archive.tar",
		"--archive-url", "https://example.com/archive.tar",
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "Only one of --archive-path or --archive-url can be specified")
}

// ---------------------------------------------------------------------------
// Tests: Upload Validation (additional rules)
// ---------------------------------------------------------------------------

func TestBbsMigrateRepo_ThrowsWhenAwsOptionsWithoutBucketName(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}
	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--aws-access-key", "some-key",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--aws-bucket-name")
}

func TestBbsMigrateRepo_ThrowsWhenGithubStorageAndAwsBucket(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}
	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--use-github-storage",
		"--aws-bucket-name", "my-bucket",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--use-github-storage")
}

func TestBbsMigrateRepo_ThrowsWhenGithubStorageAndAzure(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}
	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{azureConn: "someconn"}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--github-org", bbsGithubOrg,
		"--github-repo", bbsGithubRepo,
		"--use-github-storage",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--use-github-storage")
}

func TestBbsMigrateRepo_ThrowsWhenSmbPasswordWithoutSmbUser(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	fs := &mockBbsFileSystem{fileExistsVal: true, dirExistsVal: true}
	cmd := newBbsMigrateRepoCmd(&mockBbsGitHub{}, &mockBbsAPI{}, &mockBbsDownloader{}, &mockBbsUploader{}, fs, &mockBbsEnvProvider{}, log, defaultBbsOpts())
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--bbs-server-url", bbsServerURL,
		"--bbs-username", bbsUsername,
		"--bbs-password", bbsPassword,
		"--bbs-project", bbsProject,
		"--bbs-repo", bbsRepo,
		"--smb-password", "some-pass",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "must be specified for SMB download")
}

// ---------------------------------------------------------------------------
// Tests: buildBbsRepoURL helper
// ---------------------------------------------------------------------------

func TestBuildBbsRepoURL(t *testing.T) {
	t.Run("constructs URL when all parts provided", func(t *testing.T) {
		url := buildBbsRepoURL(bbsServerURL, bbsProject, bbsRepo)
		assert.Equal(t, bbsRepoURL, url)
	})

	t.Run("returns not-used when bbsServerURL is empty", func(t *testing.T) {
		url := buildBbsRepoURL("", bbsProject, bbsRepo)
		assert.Equal(t, "https://not-used", url)
	})

	t.Run("trims trailing slash", func(t *testing.T) {
		url := buildBbsRepoURL(bbsServerURL+"/", bbsProject, bbsRepo)
		assert.Equal(t, bbsRepoURL, url)
	})
}

// ---------------------------------------------------------------------------
// Tests: Phase Predicates
// ---------------------------------------------------------------------------

func TestBbsMigrateRepoArgs_PhasePredicates(t *testing.T) {
	t.Run("shouldGenerateArchive", func(t *testing.T) {
		a := &bbsMigrateRepoArgs{bbsServerURL: bbsServerURL}
		assert.True(t, a.shouldGenerateArchive())

		a.archivePath = bbsArchivePath
		assert.False(t, a.shouldGenerateArchive())

		a.archivePath = ""
		a.archiveURL = bbsArchiveURL
		assert.False(t, a.shouldGenerateArchive())
	})

	t.Run("shouldDownloadArchive", func(t *testing.T) {
		a := &bbsMigrateRepoArgs{sshUser: bbsSSHUser}
		assert.True(t, a.shouldDownloadArchive())

		a = &bbsMigrateRepoArgs{smbUser: bbsSMBUser}
		assert.True(t, a.shouldDownloadArchive())

		a = &bbsMigrateRepoArgs{}
		assert.False(t, a.shouldDownloadArchive())
	})

	t.Run("shouldUploadArchive", func(t *testing.T) {
		a := &bbsMigrateRepoArgs{githubOrg: bbsGithubOrg}
		assert.True(t, a.shouldUploadArchive())

		a.archiveURL = bbsArchiveURL
		assert.False(t, a.shouldUploadArchive())
	})

	t.Run("shouldImportArchive", func(t *testing.T) {
		a := &bbsMigrateRepoArgs{archiveURL: bbsArchiveURL}
		assert.True(t, a.shouldImportArchive())

		a = &bbsMigrateRepoArgs{githubOrg: bbsGithubOrg}
		assert.True(t, a.shouldImportArchive())

		a = &bbsMigrateRepoArgs{}
		assert.False(t, a.shouldImportArchive())
	})
}
