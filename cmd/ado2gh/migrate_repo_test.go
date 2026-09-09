package main

import (
	"bytes"
	"context"
	"fmt"
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

type mockAdoMigrateGitHub struct {
	// GetOrganizationId
	getOrgIDResult string
	getOrgIDErr    error

	// CreateAdoMigrationSource
	createMigSourceResult string
	createMigSourceErr    error
	createMigSourceOrgID  string
	createMigSourceURL    string

	// StartMigration
	startMigResult string
	startMigErr    error
	startMigCalled bool
	startMigOpts   []github.StartMigrationOption
	startMigSrcURL string
	startMigSrcTok string
	startMigTgtTok string

	// GetMigration
	getMigResults   []*github.Migration
	getMigErrors    []error
	getMigCallCount int
}

func (m *mockAdoMigrateGitHub) GetOrganizationId(_ context.Context, _ string) (string, error) {
	return m.getOrgIDResult, m.getOrgIDErr
}

func (m *mockAdoMigrateGitHub) CreateAdoMigrationSource(_ context.Context, orgID, adoServerURL string) (string, error) {
	m.createMigSourceOrgID = orgID
	m.createMigSourceURL = adoServerURL
	return m.createMigSourceResult, m.createMigSourceErr
}

func (m *mockAdoMigrateGitHub) StartMigration(_ context.Context, _, srcURL, _, _, srcTok, tgtTok string, opts ...github.StartMigrationOption) (string, error) {
	m.startMigCalled = true
	m.startMigSrcURL = srcURL
	m.startMigSrcTok = srcTok
	m.startMigTgtTok = tgtTok
	m.startMigOpts = opts
	return m.startMigResult, m.startMigErr
}

func (m *mockAdoMigrateGitHub) GetMigration(_ context.Context, _ string) (*github.Migration, error) {
	i := m.getMigCallCount
	m.getMigCallCount++
	if i < len(m.getMigResults) {
		var err error
		if i < len(m.getMigErrors) {
			err = m.getMigErrors[i]
		}
		return m.getMigResults[i], err
	}
	return nil, fmt.Errorf("unexpected call to GetMigration (call %d)", i)
}

type mockAdoEnvProvider struct {
	targetPAT string
	adoPAT    string
}

func (m *mockAdoEnvProvider) TargetGitHubPAT() string { return m.targetPAT }
func (m *mockAdoEnvProvider) ADOPAT() string          { return m.adoPAT }

// ---------------------------------------------------------------------------
// Tests: C# scenario 1 — Happy Path (QueueOnly)
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_HappyPath_QueueOnly(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID_123",
		createMigSourceResult: "MS_456",
		startMigResult:        "RM_789",
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Migrating Repo...")
	assert.Contains(t, output, "A repository migration (ID: RM_789) was successfully queued.")
	assert.Equal(t, 0, gh.getMigCallCount, "GetMigration should not be called in queue-only mode")
}

// ---------------------------------------------------------------------------
// Tests: C# scenario 2 — ADO Server Migration
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_AdoServerMigration(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID",
		createMigSourceResult: "MS_ID",
		startMigResult:        "RM_ID",
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
		"--ado-server-url", "https://ado.contoso.com",
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	// Verify custom ADO server URL was passed to CreateAdoMigrationSource
	assert.Equal(t, "https://ado.contoso.com", gh.createMigSourceURL)
	// Verify source repo URL uses the custom server URL
	assert.Contains(t, gh.startMigSrcURL, "https://ado.contoso.com/my-ado-org/my-project/_git/my-repo")
}

// ---------------------------------------------------------------------------
// Tests: C# scenario 3 — Skip Migration If Target Repo Exists
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_SkipIfTargetRepoExists(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID",
		createMigSourceResult: "MS_ID",
		startMigErr:           fmt.Errorf("A repository called target-org/target-repo already exists"),
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.NoError(t, err) // should NOT error

	output := buf.String()
	assert.Contains(t, output, "already contains a repository with the name")
}

// ---------------------------------------------------------------------------
// Tests: C# scenario 4 — Happy Path With Wait (poll loop)
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_HappyPathWithWait(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID",
		createMigSourceResult: "MS_ID",
		startMigResult:        "RM_POLL",
		getMigResults: []*github.Migration{
			{State: "IN_PROGRESS", RepositoryName: "target-repo"},
			{State: "IN_PROGRESS", RepositoryName: "target-repo"},
			{State: "SUCCEEDED", RepositoryName: "target-repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{pollInterval: time.Millisecond})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, 3, gh.getMigCallCount, "GetMigration should be called multiple times during polling")
	output := buf.String()
	assert.Contains(t, output, "Migration completed (ID: RM_POLL)! State: SUCCEEDED")
	assert.Contains(t, output, "Migration log available at")
}

// ---------------------------------------------------------------------------
// Tests: C# scenario 5 — Decorated error when CreateMigrationSource fails with permissions
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_PermissionsError(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:     "ORG_ID",
		createMigSourceErr: fmt.Errorf("not have the correct permissions to execute"),
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "not have the correct permissions")
	assert.Contains(t, err.Error(), "you are a member of the `target-org` organization")
}

// ---------------------------------------------------------------------------
// Tests: C# scenario 6 — Falls back to environment PATs when not provided via flags
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_FallsBackToEnvPATs(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID",
		createMigSourceResult: "MS_ID",
		startMigResult:        "RM_ID",
	}

	envProv := &mockAdoEnvProvider{
		targetPAT: "env-gh-token",
		adoPAT:    "env-ado-token",
	}

	cmd := newMigrateRepoCmd(gh, envProv, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	// Verify that the env-derived tokens were passed to StartMigration
	assert.Equal(t, "env-ado-token", gh.startMigSrcTok)
	assert.Equal(t, "env-gh-token", gh.startMigTgtTok)
}

// ---------------------------------------------------------------------------
// Tests: C# scenario 7 — Sets target repo visibility when specified
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_SetsTargetRepoVisibility(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID",
		createMigSourceResult: "MS_ID",
		startMigResult:        "RM_ID",
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
		"--target-repo-visibility", "private",
		"--queue-only",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	// Verify that an option was passed (we check that opts is non-empty)
	assert.NotEmpty(t, gh.startMigOpts, "StartMigration should have been called with visibility option")
}

// ---------------------------------------------------------------------------
// Tests: Additional — URL validation for github-org and github-repo
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_URLValidation(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "github-org is URL",
			args: []string{
				"--ado-org", "org", "--ado-team-project", "proj", "--ado-repo", "repo",
				"--github-org", "https://github.com/my-org",
				"--github-repo", "target-repo",
			},
			wantErr: "--github-org expects a name, not a URL",
		},
		{
			name: "github-repo is URL",
			args: []string{
				"--ado-org", "org", "--ado-team-project", "proj", "--ado-repo", "repo",
				"--github-org", "target-org",
				"--github-repo", "https://github.com/org/repo",
			},
			wantErr: "--github-repo expects a name, not a URL",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)
			gh := &mockAdoMigrateGitHub{}

			cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{}, log, adoMigrateRepoOptions{})
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

// ---------------------------------------------------------------------------
// Tests: Migration failure
// ---------------------------------------------------------------------------

func TestAdoMigrateRepo_MigrationFails(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	gh := &mockAdoMigrateGitHub{
		getOrgIDResult:        "ORG_ID",
		createMigSourceResult: "MS_ID",
		startMigResult:        "RM_FAIL",
		getMigResults: []*github.Migration{
			{State: "FAILED", RepositoryName: "target-repo", FailureReason: "something broke", WarningsCount: 3, MigrationLogURL: "https://example.com/log"},
		},
	}

	cmd := newMigrateRepoCmd(gh, &mockAdoEnvProvider{targetPAT: "gh-token", adoPAT: "ado-token"}, log, adoMigrateRepoOptions{})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "something broke")

	output := buf.String()
	assert.Contains(t, output, "Migration Failed. Migration ID: RM_FAIL")
	assert.Contains(t, output, "3 warnings")
}
