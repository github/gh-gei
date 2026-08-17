package main

import (
	"bytes"
	"context"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockShareServiceConnectionAdoAPI struct {
	getTeamProjectIDFn     func(ctx context.Context, org, teamProject string) (string, error)
	containsServiceConnFn  func(ctx context.Context, org, teamProject, serviceConnectionId string) (bool, error)
	shareServiceConnFn     func(ctx context.Context, org, teamProject, teamProjectId, serviceConnectionId string) error
	shareServiceConnCalled bool
}

func (m *mockShareServiceConnectionAdoAPI) GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error) {
	return m.getTeamProjectIDFn(ctx, org, teamProject)
}

func (m *mockShareServiceConnectionAdoAPI) ContainsServiceConnection(ctx context.Context, org, teamProject, serviceConnectionId string) (bool, error) {
	return m.containsServiceConnFn(ctx, org, teamProject, serviceConnectionId)
}

func (m *mockShareServiceConnectionAdoAPI) ShareServiceConnection(ctx context.Context, org, teamProject, teamProjectId, serviceConnectionId string) error {
	m.shareServiceConnCalled = true
	return m.shareServiceConnFn(ctx, org, teamProject, teamProjectId, serviceConnectionId)
}

type mockShareServiceConnectionEnvProvider struct {
	adoPAT string
}

func (m *mockShareServiceConnectionEnvProvider) ADOPAT() string { return m.adoPAT }

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestShareServiceConnection_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockShareServiceConnectionAdoAPI{
		getTeamProjectIDFn: func(_ context.Context, _, _ string) (string, error) {
			return "tp-id-123", nil
		},
		containsServiceConnFn: func(_ context.Context, _, _, _ string) (bool, error) {
			return false, nil
		},
		shareServiceConnFn: func(_ context.Context, _, _, _, _ string) error {
			return nil
		},
	}

	cmd := newShareServiceConnectionCmd(adoAPI, &mockShareServiceConnectionEnvProvider{adoPAT: "ado-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--service-connection-id", "sc-123",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.True(t, adoAPI.shareServiceConnCalled, "ShareServiceConnection should be called")
	output := buf.String()
	assert.Contains(t, output, "Sharing Service Connection...")
	assert.Contains(t, output, "Successfully shared service connection")
}

func TestShareServiceConnection_SkipsWhenAlreadyShared(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockShareServiceConnectionAdoAPI{
		getTeamProjectIDFn: func(_ context.Context, _, _ string) (string, error) {
			return "tp-id-123", nil
		},
		containsServiceConnFn: func(_ context.Context, _, _, _ string) (bool, error) {
			return true, nil
		},
		shareServiceConnFn: func(_ context.Context, _, _, _, _ string) error {
			t.Fatal("ShareServiceConnection should not be called when already shared")
			return nil
		},
	}

	cmd := newShareServiceConnectionCmd(adoAPI, &mockShareServiceConnectionEnvProvider{adoPAT: "ado-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--service-connection-id", "sc-123",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.False(t, adoAPI.shareServiceConnCalled, "ShareServiceConnection should not be called")
	output := buf.String()
	assert.Contains(t, output, "Service connection already shared with team project")
}
