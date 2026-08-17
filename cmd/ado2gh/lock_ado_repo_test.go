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

type mockLockAdoRepoAPI struct {
	// GetTeamProjectId
	getTeamProjectIdFn func(ctx context.Context, org, teamProject string) (string, error)

	// GetRepoId
	getRepoIdFn func(ctx context.Context, org, teamProject, repo string) (string, error)

	// GetIdentityDescriptor
	getIdentityDescriptorFn func(ctx context.Context, org, teamProjectId, groupName string) (string, error)

	// LockRepo
	lockRepoFn     func(ctx context.Context, org, teamProjectId, repoId, identityDescriptor string) error
	lockRepoCalled bool
	lockRepoOrg    string
	lockRepoProjID string
	lockRepoRepoID string
	lockRepoIdDesc string
}

func (m *mockLockAdoRepoAPI) GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error) {
	return m.getTeamProjectIdFn(ctx, org, teamProject)
}

func (m *mockLockAdoRepoAPI) GetRepoId(ctx context.Context, org, teamProject, repo string) (string, error) {
	return m.getRepoIdFn(ctx, org, teamProject, repo)
}

func (m *mockLockAdoRepoAPI) GetIdentityDescriptor(ctx context.Context, org, teamProjectId, groupName string) (string, error) {
	return m.getIdentityDescriptorFn(ctx, org, teamProjectId, groupName)
}

func (m *mockLockAdoRepoAPI) LockRepo(ctx context.Context, org, teamProjectId, repoId, identityDescriptor string) error {
	m.lockRepoCalled = true
	m.lockRepoOrg = org
	m.lockRepoProjID = teamProjectId
	m.lockRepoRepoID = repoId
	m.lockRepoIdDesc = identityDescriptor
	if m.lockRepoFn != nil {
		return m.lockRepoFn(ctx, org, teamProjectId, repoId, identityDescriptor)
	}
	return nil
}

type mockLockAdoRepoEnv struct {
	adoPAT string
}

func (m *mockLockAdoRepoEnv) ADOPAT() string { return m.adoPAT }

// ---------------------------------------------------------------------------
// Tests: Happy Path
// ---------------------------------------------------------------------------

func TestLockAdoRepo_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockLockAdoRepoAPI{
		getTeamProjectIdFn: func(_ context.Context, _, _ string) (string, error) {
			return "team-project-id", nil
		},
		getRepoIdFn: func(_ context.Context, _, _, _ string) (string, error) {
			return "repo-id", nil
		},
		getIdentityDescriptorFn: func(_ context.Context, _, _, _ string) (string, error) {
			return "identity-descriptor", nil
		},
	}

	cmd := newLockAdoRepoCmd(adoAPI, &mockLockAdoRepoEnv{adoPAT: "ado-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
		"--ado-repo", "my-repo",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Locking repo...")
	assert.Contains(t, output, "Repo successfully locked")

	// Verify LockRepo was called with correct args
	assert.True(t, adoAPI.lockRepoCalled)
	assert.Equal(t, "my-ado-org", adoAPI.lockRepoOrg)
	assert.Equal(t, "team-project-id", adoAPI.lockRepoProjID)
	assert.Equal(t, "repo-id", adoAPI.lockRepoRepoID)
	assert.Equal(t, "identity-descriptor", adoAPI.lockRepoIdDesc)
}
