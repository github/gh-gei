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

// mockMigrationWaiter implements the migrationWaiter interface for testing.
type mockMigrationWaiter struct {
	getMigrationResults      []*github.Migration
	getMigrationErrors       []error
	getMigrationCallCount    int
	getOrgMigrationResults   []*github.OrgMigration
	getOrgMigrationErrors    []error
	getOrgMigrationCallCount int
}

func (m *mockMigrationWaiter) GetMigration(_ context.Context, _ string) (*github.Migration, error) {
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

func (m *mockMigrationWaiter) GetOrganizationMigration(_ context.Context, _ string) (*github.OrgMigration, error) {
	i := m.getOrgMigrationCallCount
	m.getOrgMigrationCallCount++
	if i < len(m.getOrgMigrationResults) {
		var err error
		if i < len(m.getOrgMigrationErrors) {
			err = m.getOrgMigrationErrors[i]
		}
		return m.getOrgMigrationResults[i], err
	}
	return nil, fmt.Errorf("unexpected call to GetOrganizationMigration (call %d)", i)
}

func TestWaitForMigration(t *testing.T) {
	tests := []struct {
		name        string
		migrationID string
		mock        *mockMigrationWaiter
		wantErr     string
		wantOutput  []string // substrings that must appear in output
	}{
		{
			name:        "repo migration succeeds immediately",
			migrationID: "RM_123",
			mock: &mockMigrationWaiter{
				getMigrationResults: []*github.Migration{
					{State: "SUCCEEDED", RepositoryName: "my-repo", WarningsCount: 0, MigrationLogURL: "https://example.com/log"},
				},
			},
			wantOutput: []string{"succeeded for my-repo", "Migration log available at https://example.com/log"},
		},
		{
			name:        "repo migration succeeds after 2 polls",
			migrationID: "RM_456",
			mock: &mockMigrationWaiter{
				getMigrationResults: []*github.Migration{
					{State: "IN_PROGRESS", RepositoryName: "my-repo"},
					{State: "IN_PROGRESS", RepositoryName: "my-repo"},
					{State: "SUCCEEDED", RepositoryName: "my-repo", WarningsCount: 2, MigrationLogURL: "https://example.com/log"},
				},
			},
			wantOutput: []string{"succeeded for my-repo", "2 warnings encountered during this migration"},
		},
		{
			name:        "repo migration fails",
			migrationID: "RM_789",
			mock: &mockMigrationWaiter{
				getMigrationResults: []*github.Migration{
					{State: "FAILED", RepositoryName: "my-repo", FailureReason: "something broke", WarningsCount: 1, MigrationLogURL: "https://example.com/log"},
				},
			},
			wantErr:    "something broke",
			wantOutput: []string{"failed for my-repo", "1 warning encountered during this migration"},
		},
		{
			name:        "org migration succeeds immediately",
			migrationID: "OM_100",
			mock: &mockMigrationWaiter{
				getOrgMigrationResults: []*github.OrgMigration{
					{State: "SUCCEEDED", SourceOrgURL: "https://github.com/src-org", TargetOrgName: "target-org"},
				},
			},
			wantOutput: []string{"succeeded"},
		},
		{
			name:        "org migration fails",
			migrationID: "OM_200",
			mock: &mockMigrationWaiter{
				getOrgMigrationResults: []*github.OrgMigration{
					{State: "FAILED", SourceOrgURL: "https://github.com/src-org", TargetOrgName: "target-org", FailureReason: "org migration broke"},
				},
			},
			wantErr: "org migration broke",
		},
		{
			name:        "org migration in repo_migration phase shows progress",
			migrationID: "OM_300",
			mock: &mockMigrationWaiter{
				getOrgMigrationResults: []*github.OrgMigration{
					{State: "REPO_MIGRATION", SourceOrgURL: "https://github.com/src-org", TargetOrgName: "target-org", TotalRepositoriesCount: 10, RemainingRepositoriesCount: 7},
					{State: "SUCCEEDED", SourceOrgURL: "https://github.com/src-org", TargetOrgName: "target-org"},
				},
			},
			wantOutput: []string{"3/10 repositories completed", "succeeded"},
		},
		{
			name:        "invalid migration ID prefix",
			migrationID: "XX_invalid",
			mock:        &mockMigrationWaiter{},
			wantErr:     "Invalid migration id: XX_invalid",
		},
		{
			name:        "missing migration ID",
			migrationID: "",
			mock:        &mockMigrationWaiter{},
			wantErr:     "--migration-id must be provided",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			cmd := newWaitForMigrationCmd(tc.mock, log, 0)
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)

			args := []string{}
			if tc.migrationID != "" {
				args = append(args, "--migration-id", tc.migrationID)
			}
			cmd.SetArgs(args)

			err := cmd.Execute()

			if tc.wantErr != "" {
				require.Error(t, err)
				assert.Contains(t, err.Error(), tc.wantErr)
			} else {
				require.NoError(t, err)
			}

			output := buf.String()
			for _, want := range tc.wantOutput {
				assert.Contains(t, output, want, "expected output to contain %q", want)
			}
		})
	}
}

func TestWaitForMigration_RepoMigrationWarningCounts(t *testing.T) {
	tests := []struct {
		name          string
		warningsCount int
		wantWarning   string
		wantNoWarning bool
	}{
		{
			name:          "zero warnings logs nothing",
			warningsCount: 0,
			wantNoWarning: true,
		},
		{
			name:          "one warning logs singular",
			warningsCount: 1,
			wantWarning:   "1 warning encountered during this migration",
		},
		{
			name:          "multiple warnings logs plural",
			warningsCount: 5,
			wantWarning:   "5 warnings encountered during this migration",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			mock := &mockMigrationWaiter{
				getMigrationResults: []*github.Migration{
					{State: "SUCCEEDED", RepositoryName: "repo", WarningsCount: tc.warningsCount, MigrationLogURL: "https://example.com/log"},
				},
			}

			cmd := newWaitForMigrationCmd(mock, log, 0)
			cmd.SetArgs([]string{"--migration-id", "RM_test"})

			err := cmd.Execute()
			require.NoError(t, err)

			output := buf.String()
			if tc.wantNoWarning {
				assert.NotContains(t, output, "warning")
			} else {
				assert.Contains(t, output, tc.wantWarning)
			}
		})
	}
}

// Verify that polling actually happens (calls > 1 when initial state is pending).
func TestWaitForMigration_RepoPolling(t *testing.T) {
	mock := &mockMigrationWaiter{
		getMigrationResults: []*github.Migration{
			{State: "IN_PROGRESS", RepositoryName: "repo"},
			{State: "SUCCEEDED", RepositoryName: "repo", MigrationLogURL: "https://example.com/log"},
		},
	}

	var buf bytes.Buffer
	log := logger.New(false, &buf)
	cmd := newWaitForMigrationCmd(mock, log, time.Duration(0))
	cmd.SetArgs([]string{"--migration-id", "RM_poll"})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, 2, mock.getMigrationCallCount, "expected 2 calls to GetMigration for polling")
}

// Verify org polling calls > 1 when initial state is pending.
func TestWaitForMigration_OrgPolling(t *testing.T) {
	mock := &mockMigrationWaiter{
		getOrgMigrationResults: []*github.OrgMigration{
			{State: "IN_PROGRESS", SourceOrgURL: "https://github.com/org", TargetOrgName: "target"},
			{State: "SUCCEEDED", SourceOrgURL: "https://github.com/org", TargetOrgName: "target"},
		},
	}

	var buf bytes.Buffer
	log := logger.New(false, &buf)
	cmd := newWaitForMigrationCmd(mock, log, time.Duration(0))
	cmd.SetArgs([]string{"--migration-id", "OM_poll"})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.Equal(t, 2, mock.getOrgMigrationCallCount, "expected 2 calls to GetOrganizationMigration for polling")
}

// Verify that context cancellation stops the polling loop.
func TestWaitForMigration_ContextCancellation(t *testing.T) {
	mock := &mockMigrationWaiter{
		getMigrationResults: []*github.Migration{
			{State: "IN_PROGRESS", RepositoryName: "repo"},
			{State: "IN_PROGRESS", RepositoryName: "repo"},
			{State: "IN_PROGRESS", RepositoryName: "repo"},
		},
	}

	var buf bytes.Buffer
	log := logger.New(false, &buf)

	ctx, cancel := context.WithCancel(context.Background())
	// Cancel immediately so the poll loop exits on the first select.
	cancel()

	err := runWaitForMigration(ctx, mock, log, "RM_cancel", 10*time.Second)
	require.Error(t, err)
	assert.ErrorIs(t, err, context.Canceled)
}
