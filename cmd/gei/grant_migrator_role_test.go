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

// mockMigratorRoleGranter implements migratorRoleGranter for testing.
type mockMigratorRoleGranter struct {
	orgID       string
	orgIDErr    error
	grantResult bool
	grantErr    error
	gotOrg      string
	gotOrgID    string
	gotActor    string
	gotType     string
}

func (m *mockMigratorRoleGranter) GetOrganizationId(_ context.Context, org string) (string, error) {
	m.gotOrg = org
	return m.orgID, m.orgIDErr
}

func (m *mockMigratorRoleGranter) GrantMigratorRole(_ context.Context, orgID, actor, actorType string) (bool, error) {
	m.gotOrgID = orgID
	m.gotActor = actor
	m.gotType = actorType
	return m.grantResult, m.grantErr
}

func TestGrantMigratorRole(t *testing.T) {
	tests := []struct {
		name       string
		args       []string
		mock       *mockMigratorRoleGranter
		wantErr    string
		wantOutput []string
		assertArgs func(t *testing.T, m *mockMigratorRoleGranter)
	}{
		{
			name: "grant succeeds",
			args: []string{"--github-org", "my-org", "--actor", "monalisa", "--actor-type", "USER"},
			mock: &mockMigratorRoleGranter{orgID: "ORG_ID_123", grantResult: true},
			wantOutput: []string{
				"Granting migrator role ...",
				`Migrator role successfully set for the USER "monalisa"`,
			},
			assertArgs: func(t *testing.T, m *mockMigratorRoleGranter) {
				assert.Equal(t, "my-org", m.gotOrg)
				assert.Equal(t, "ORG_ID_123", m.gotOrgID)
				assert.Equal(t, "monalisa", m.gotActor)
				assert.Equal(t, "USER", m.gotType)
			},
		},
		{
			name: "grant succeeds with lowercase actor type",
			args: []string{"--github-org", "my-org", "--actor", "my-team", "--actor-type", "team"},
			mock: &mockMigratorRoleGranter{orgID: "ORG_ID_123", grantResult: true},
			wantOutput: []string{
				`Migrator role successfully set for the TEAM "my-team"`,
			},
		},
		{
			name: "grant fails returns false",
			args: []string{"--github-org", "my-org", "--actor", "monalisa", "--actor-type", "USER"},
			mock: &mockMigratorRoleGranter{orgID: "ORG_ID_123", grantResult: false},
			wantOutput: []string{
				`Migrator role couldn't be set for the USER "monalisa"`,
			},
		},
		{
			name:    "GetOrganizationId error propagates",
			args:    []string{"--github-org", "my-org", "--actor", "monalisa", "--actor-type", "USER"},
			mock:    &mockMigratorRoleGranter{orgIDErr: fmt.Errorf("org not found")},
			wantErr: "org not found",
		},
		{
			name:    "invalid actor type",
			args:    []string{"--github-org", "my-org", "--actor", "monalisa", "--actor-type", "INVALID"},
			mock:    &mockMigratorRoleGranter{},
			wantErr: "Actor type must be either TEAM or USER.",
		},
		{
			name:    "github-org is URL",
			args:    []string{"--github-org", "https://github.com/my-org", "--actor", "monalisa", "--actor-type", "USER"},
			mock:    &mockMigratorRoleGranter{},
			wantErr: "The --github-org option expects an organization name, not a URL",
		},
		{
			name:    "ghes-api-url and target-api-url both set",
			args:    []string{"--github-org", "my-org", "--actor", "monalisa", "--actor-type", "USER", "--ghes-api-url", "https://ghes.example.com", "--target-api-url", "https://api.github.com"},
			mock:    &mockMigratorRoleGranter{},
			wantErr: "Only one of --ghes-api-url or --target-api-url can be set at a time.",
		},
		{
			name:    "GrantMigratorRole error propagates",
			args:    []string{"--github-org", "my-org", "--actor", "monalisa", "--actor-type", "USER"},
			mock:    &mockMigratorRoleGranter{orgID: "ORG_ID_123", grantErr: fmt.Errorf("permission denied")},
			wantErr: "permission denied",
		},
		{
			name:    "empty github-org",
			args:    []string{"--github-org", "", "--actor", "monalisa", "--actor-type", "USER"},
			mock:    &mockMigratorRoleGranter{},
			wantErr: "--github-org must be provided",
		},
		{
			name:    "empty actor",
			args:    []string{"--github-org", "my-org", "--actor", "", "--actor-type", "USER"},
			mock:    &mockMigratorRoleGranter{},
			wantErr: "--actor must be provided",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			cmd := newGrantMigratorRoleCmd(tc.mock, log)
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

			if tc.assertArgs != nil {
				tc.assertArgs(t, tc.mock)
			}
		})
	}
}
