package main

import (
	"bytes"
	"context"
	"fmt"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockSecretAlertMigrator implements secretAlertMigrator for testing.
type mockSecretAlertMigrator struct {
	err           error
	called        bool
	gotSourceOrg  string
	gotSourceRepo string
	gotTargetOrg  string
	gotTargetRepo string
	gotDryRun     bool
}

func (m *mockSecretAlertMigrator) MigrateAlerts(_ context.Context, sourceOrg, sourceRepo, targetOrg, targetRepo string, dryRun bool) error {
	m.called = true
	m.gotSourceOrg = sourceOrg
	m.gotSourceRepo = sourceRepo
	m.gotTargetOrg = targetOrg
	m.gotTargetRepo = targetRepo
	m.gotDryRun = dryRun
	return m.err
}

func TestMigrateSecretAlerts(t *testing.T) {
	tests := []struct {
		name        string
		args        []string
		mock        *mockSecretAlertMigrator
		wantErr     string
		wantOutput  []string
		wantCalled  bool
		wantDryRun  bool
		wantSrcOrg  string
		wantSrcRepo string
		wantTgtOrg  string
		wantTgtRepo string
	}{
		{
			name: "successful migration",
			args: []string{
				"--source-org", "src-org",
				"--source-repo", "src-repo",
				"--target-org", "tgt-org",
				"--target-repo", "tgt-repo",
			},
			mock:        &mockSecretAlertMigrator{},
			wantOutput:  []string{"Secret scanning alerts successfully migrated"},
			wantCalled:  true,
			wantSrcOrg:  "src-org",
			wantSrcRepo: "src-repo",
			wantTgtOrg:  "tgt-org",
			wantTgtRepo: "tgt-repo",
		},
		{
			name: "dry run",
			args: []string{
				"--source-org", "src-org",
				"--source-repo", "src-repo",
				"--target-org", "tgt-org",
				"--target-repo", "tgt-repo",
				"--dry-run",
			},
			mock:       &mockSecretAlertMigrator{},
			wantCalled: true,
			wantDryRun: true,
		},
		{
			name: "target-repo defaults to source-repo",
			args: []string{
				"--source-org", "src-org",
				"--source-repo", "my-repo",
				"--target-org", "tgt-org",
			},
			mock:        &mockSecretAlertMigrator{},
			wantCalled:  true,
			wantSrcRepo: "my-repo",
			wantTgtRepo: "my-repo",
			wantOutput:  []string{"target-repo"},
		},
		{
			name:    "missing source-org",
			args:    []string{"--source-repo", "repo", "--target-org", "org"},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--source-org must be provided",
		},
		{
			name:    "missing source-repo",
			args:    []string{"--source-org", "org", "--target-org", "org"},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--source-repo must be provided",
		},
		{
			name:    "missing target-org",
			args:    []string{"--source-org", "org", "--source-repo", "repo"},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--target-org must be provided",
		},
		{
			name: "source-org rejects URL",
			args: []string{
				"--source-org", "https://github.com/my-org",
				"--source-repo", "repo",
				"--target-org", "org",
			},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--source-org expects a name, not a URL",
		},
		{
			name: "target-org rejects URL",
			args: []string{
				"--source-org", "org",
				"--source-repo", "repo",
				"--target-org", "https://github.com/tgt-org",
			},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--target-org expects a name, not a URL",
		},
		{
			name: "source-repo rejects URL",
			args: []string{
				"--source-org", "org",
				"--source-repo", "https://github.com/org/repo",
				"--target-org", "org",
			},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--source-repo expects a name, not a URL",
		},
		{
			name: "target-repo rejects URL",
			args: []string{
				"--source-org", "org",
				"--source-repo", "repo",
				"--target-org", "org",
				"--target-repo", "https://github.com/org/repo",
			},
			mock:    &mockSecretAlertMigrator{},
			wantErr: "--target-repo expects a name, not a URL",
		},
		{
			name: "service error propagated",
			args: []string{
				"--source-org", "org",
				"--source-repo", "repo",
				"--target-org", "org",
			},
			mock:       &mockSecretAlertMigrator{err: fmt.Errorf("API rate limit")},
			wantErr:    "API rate limit",
			wantCalled: true,
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			cmd := newMigrateSecretAlertsCmd(tc.mock, log)
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

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

			assert.Equal(t, tc.wantCalled, tc.mock.called, "expected MigrateAlerts called=%v", tc.wantCalled)
			if tc.wantDryRun {
				assert.True(t, tc.mock.gotDryRun, "expected dry-run=true")
			}
			if tc.wantSrcOrg != "" {
				assert.Equal(t, tc.wantSrcOrg, tc.mock.gotSourceOrg)
			}
			if tc.wantSrcRepo != "" {
				assert.Equal(t, tc.wantSrcRepo, tc.mock.gotSourceRepo)
			}
			if tc.wantTgtOrg != "" {
				assert.Equal(t, tc.wantTgtOrg, tc.mock.gotTargetOrg)
			}
			if tc.wantTgtRepo != "" {
				assert.Equal(t, tc.wantTgtRepo, tc.mock.gotTargetRepo)
			}
		})
	}
}
