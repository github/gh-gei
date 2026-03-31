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

// mockOrgMigrator implements the orgMigrator interface for testing.
type mockOrgMigrator struct {
	getEnterpriseIDResult string
	getEnterpriseIDErr    error
	getEnterpriseIDCalls  []string

	startOrgMigrationResult string
	startOrgMigrationErr    error
	startOrgMigrationCalls  []startOrgMigrationCall

	getOrgMigrationResults []*github.OrgMigration
	getOrgMigrationErrors  []error
	getOrgMigrationCount   int
}

type startOrgMigrationCall struct {
	sourceOrgURL       string
	targetOrgName      string
	targetEnterpriseID string
	sourceAccessToken  string
}

func (m *mockOrgMigrator) GetEnterpriseId(_ context.Context, enterprise string) (string, error) {
	m.getEnterpriseIDCalls = append(m.getEnterpriseIDCalls, enterprise)
	return m.getEnterpriseIDResult, m.getEnterpriseIDErr
}

func (m *mockOrgMigrator) StartOrganizationMigration(_ context.Context, sourceOrgURL, targetOrgName, targetEnterpriseID, sourceAccessToken string) (string, error) {
	m.startOrgMigrationCalls = append(m.startOrgMigrationCalls, startOrgMigrationCall{
		sourceOrgURL:       sourceOrgURL,
		targetOrgName:      targetOrgName,
		targetEnterpriseID: targetEnterpriseID,
		sourceAccessToken:  sourceAccessToken,
	})
	return m.startOrgMigrationResult, m.startOrgMigrationErr
}

func (m *mockOrgMigrator) GetOrganizationMigration(_ context.Context, _ string) (*github.OrgMigration, error) {
	i := m.getOrgMigrationCount
	m.getOrgMigrationCount++
	if i < len(m.getOrgMigrationResults) {
		var err error
		if i < len(m.getOrgMigrationErrors) {
			err = m.getOrgMigrationErrors[i]
		}
		return m.getOrgMigrationResults[i], err
	}
	return nil, fmt.Errorf("unexpected call to GetOrganizationMigration (call %d)", i)
}

// mockOrgEnvProvider implements migrateOrgEnvProvider for testing.
type mockOrgEnvProvider struct {
	sourceGitHubPAT string
	targetGitHubPAT string
}

func (m *mockOrgEnvProvider) SourceGitHubPAT() string { return m.sourceGitHubPAT }
func (m *mockOrgEnvProvider) TargetGitHubPAT() string { return m.targetGitHubPAT }

// helper to run the migrate-org command with given flags
func runMigrateOrgCmd(t *testing.T, gh *mockOrgMigrator, envProv *mockOrgEnvProvider, args ...string) (string, error) {
	t.Helper()
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	cmd := newMigrateOrgCmd(gh, envProv, log, 0)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs(args)
	err := cmd.Execute()
	return buf.String(), err
}

func TestMigrateOrg_Validation_RequiredFlags(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name:    "missing source-org",
			args:    []string{"--github-target-org", "tgt", "--github-target-enterprise", "ent"},
			wantErr: "--github-source-org must be provided",
		},
		{
			name:    "missing target-org",
			args:    []string{"--github-source-org", "src", "--github-target-enterprise", "ent"},
			wantErr: "--github-target-org must be provided",
		},
		{
			name:    "missing target-enterprise",
			args:    []string{"--github-source-org", "src", "--github-target-org", "tgt"},
			wantErr: "--github-target-enterprise must be provided",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			gh := &mockOrgMigrator{}
			env := &mockOrgEnvProvider{}
			_, err := runMigrateOrgCmd(t, gh, env, tc.args...)
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateOrg_Validation_URLRejection(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name:    "source-org is a URL",
			args:    []string{"--github-source-org", "https://github.com/foo", "--github-target-org", "tgt", "--github-target-enterprise", "ent"},
			wantErr: "--github-source-org expects a name, not a URL",
		},
		{
			name:    "target-org is a URL",
			args:    []string{"--github-source-org", "src", "--github-target-org", "https://github.com/foo", "--github-target-enterprise", "ent"},
			wantErr: "--github-target-org expects a name, not a URL",
		},
		{
			name:    "target-enterprise is a URL",
			args:    []string{"--github-source-org", "src", "--github-target-org", "tgt", "--github-target-enterprise", "https://github.com/enterprises/foo"},
			wantErr: "--github-target-enterprise expects a name, not a URL",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			gh := &mockOrgMigrator{}
			env := &mockOrgEnvProvider{}
			_, err := runMigrateOrgCmd(t, gh, env, tc.args...)
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}

func TestMigrateOrg_QueueOnly(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_456",
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "env-source-pat"}

	output, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "my-enterprise",
		"--queue-only",
	)

	require.NoError(t, err)
	assert.Contains(t, output, "OM_456")
	assert.Contains(t, output, "successfully queued")

	// Verify the source org URL was correctly built
	require.Len(t, gh.startOrgMigrationCalls, 1)
	assert.Equal(t, "https://github.com/src-org", gh.startOrgMigrationCalls[0].sourceOrgURL)
	assert.Equal(t, "tgt-org", gh.startOrgMigrationCalls[0].targetOrgName)
	assert.Equal(t, "ENT_123", gh.startOrgMigrationCalls[0].targetEnterpriseID)
	assert.Equal(t, "env-source-pat", gh.startOrgMigrationCalls[0].sourceAccessToken)

	// Verify GetEnterpriseId was called with the enterprise name
	require.Len(t, gh.getEnterpriseIDCalls, 1)
	assert.Equal(t, "my-enterprise", gh.getEnterpriseIDCalls[0])

	// Verify no polling happened
	assert.Equal(t, 0, gh.getOrgMigrationCount)
}

func TestMigrateOrg_FullMigrationSucceeds(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_789",
		getOrgMigrationResults: []*github.OrgMigration{
			{State: "IN_PROGRESS"},
			{State: "SUCCEEDED"},
		},
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "src-pat"}

	output, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "my-enterprise",
	)

	require.NoError(t, err)
	assert.Contains(t, output, "Migration completed")
	assert.Contains(t, output, "OM_789")
	assert.Equal(t, 2, gh.getOrgMigrationCount)
}

func TestMigrateOrg_RepoMigrationStateShowsProgress(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_100",
		getOrgMigrationResults: []*github.OrgMigration{
			{State: "REPO_MIGRATION", TotalRepositoriesCount: 10, RemainingRepositoriesCount: 7},
			{State: "SUCCEEDED"},
		},
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	output, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
	)

	require.NoError(t, err)
	assert.Contains(t, output, "3/10")
	assert.Contains(t, output, "Migration completed")
}

func TestMigrateOrg_MigrationFails(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_fail",
		getOrgMigrationResults: []*github.OrgMigration{
			{State: "FAILED", FailureReason: "something went wrong"},
		},
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	output, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
	)

	require.Error(t, err)
	assert.Contains(t, err.Error(), "something went wrong")
	assert.Contains(t, output, "Migration Failed")
}

func TestMigrateOrg_SourceTokenFromFlag(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_1",
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "env-pat"}

	_, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
		"--github-source-pat", "flag-pat",
		"--queue-only",
	)

	require.NoError(t, err)
	require.Len(t, gh.startOrgMigrationCalls, 1)
	assert.Equal(t, "flag-pat", gh.startOrgMigrationCalls[0].sourceAccessToken)
}

func TestMigrateOrg_SourceTokenFallsBackToEnv(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_1",
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "env-pat"}

	_, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
		"--queue-only",
	)

	require.NoError(t, err)
	require.Len(t, gh.startOrgMigrationCalls, 1)
	assert.Equal(t, "env-pat", gh.startOrgMigrationCalls[0].sourceAccessToken)
}

func TestMigrateOrg_TargetPATFallsToSourcePAT(t *testing.T) {
	// When --github-target-pat is set but --github-source-pat is not,
	// source-pat should default to the target-pat value.
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_1",
	}
	envProv := &mockOrgEnvProvider{}

	output, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
		"--github-target-pat", "target-pat-val",
		"--queue-only",
	)

	require.NoError(t, err)
	require.Len(t, gh.startOrgMigrationCalls, 1)
	assert.Equal(t, "target-pat-val", gh.startOrgMigrationCalls[0].sourceAccessToken)
	assert.Contains(t, output, "github-source-pat will also use its value")
}

func TestMigrateOrg_SourceOrgURLEscapesSpecialChars(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_1",
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	_, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "org with spaces",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
		"--queue-only",
	)

	require.NoError(t, err)
	require.Len(t, gh.startOrgMigrationCalls, 1)
	assert.Equal(t, "https://github.com/org%20with%20spaces", gh.startOrgMigrationCalls[0].sourceOrgURL)
}

func TestMigrateOrg_GetEnterpriseIdError(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDErr: fmt.Errorf("enterprise not found"),
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	_, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
	)

	require.Error(t, err)
	assert.Contains(t, err.Error(), "enterprise not found")
}

func TestMigrateOrg_StartMigrationError(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult: "ENT_123",
		startOrgMigrationErr:  fmt.Errorf("start failed"),
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	_, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
	)

	require.Error(t, err)
	assert.Contains(t, err.Error(), "start failed")
}

func TestMigrateOrg_ContextCancellation(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_1",
		getOrgMigrationResults: []*github.OrgMigration{
			{State: "IN_PROGRESS"},
			{State: "IN_PROGRESS"},
			{State: "IN_PROGRESS"},
		},
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	var buf bytes.Buffer
	log := logger.New(false, &buf)

	ctx, cancel := context.WithCancel(context.Background())
	cancel() // cancel immediately

	err := runMigrateOrg(ctx, migrateOrgArgs{
		githubSourceOrg:        "src-org",
		githubTargetOrg:        "tgt-org",
		githubTargetEnterprise: "ent",
		githubSourcePAT:        "pat",
	}, gh, envProv, log, 10_000_000_000) // 10s poll - but context is already canceled

	require.Error(t, err)
	assert.ErrorIs(t, err, context.Canceled)
}

func TestMigrateOrg_PollingMultipleTimes(t *testing.T) {
	gh := &mockOrgMigrator{
		getEnterpriseIDResult:   "ENT_123",
		startOrgMigrationResult: "OM_1",
		getOrgMigrationResults: []*github.OrgMigration{
			{State: "PRE_REPO_MIGRATION"},
			{State: "REPO_MIGRATION", TotalRepositoriesCount: 5, RemainingRepositoriesCount: 3},
			{State: "POST_REPO_MIGRATION"},
			{State: "SUCCEEDED"},
		},
	}
	envProv := &mockOrgEnvProvider{sourceGitHubPAT: "pat"}

	output, err := runMigrateOrgCmd(t, gh, envProv,
		"--github-source-org", "src-org",
		"--github-target-org", "tgt-org",
		"--github-target-enterprise", "ent",
	)

	require.NoError(t, err)
	assert.Equal(t, 4, gh.getOrgMigrationCount)
	assert.Contains(t, output, "2/5")
	assert.Contains(t, output, "Migration completed")
}
