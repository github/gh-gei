package main

import (
	"bytes"
	"context"
	"testing"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockDisableAdoRepoAPI struct {
	// GetRepos
	getReposFn func(ctx context.Context, org, teamProject string) ([]ado.Repository, error)

	// DisableRepo
	disableRepoFn     func(ctx context.Context, org, teamProject, repoId string) error
	disableRepoCalled bool
	disableRepoOrg    string
	disableRepoProj   string
	disableRepoID     string
}

func (m *mockDisableAdoRepoAPI) GetRepos(ctx context.Context, org, teamProject string) ([]ado.Repository, error) {
	return m.getReposFn(ctx, org, teamProject)
}

func (m *mockDisableAdoRepoAPI) DisableRepo(ctx context.Context, org, teamProject, repoId string) error {
	m.disableRepoCalled = true
	m.disableRepoOrg = org
	m.disableRepoProj = teamProject
	m.disableRepoID = repoId
	if m.disableRepoFn != nil {
		return m.disableRepoFn(ctx, org, teamProject, repoId)
	}
	return nil
}

type mockDisableAdoRepoEnv struct {
	adoPAT string
}

func (m *mockDisableAdoRepoEnv) ADOPAT() string { return m.adoPAT }

// ---------------------------------------------------------------------------
// Tests: Happy Path
// ---------------------------------------------------------------------------

func TestDisableAdoRepo_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockDisableAdoRepoAPI{
		getReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{
				{ID: "repo-id-1", Name: "my-repo", IsDisabled: false},
			}, nil
		},
	}

	cmd := newDisableAdoRepoCmd(adoAPI, &mockDisableAdoRepoEnv{adoPAT: "ado-token"}, log)
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
	assert.Contains(t, output, "Disabling repo...")
	assert.Contains(t, output, "Repo successfully disabled")

	// Verify DisableRepo was called with correct args
	assert.True(t, adoAPI.disableRepoCalled)
	assert.Equal(t, "my-ado-org", adoAPI.disableRepoOrg)
	assert.Equal(t, "my-project", adoAPI.disableRepoProj)
	assert.Equal(t, "repo-id-1", adoAPI.disableRepoID)
}

// ---------------------------------------------------------------------------
// Tests: Idempotency — Repo Already Disabled
// ---------------------------------------------------------------------------

func TestDisableAdoRepo_IdempotencyRepoDisabled(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockDisableAdoRepoAPI{
		getReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{
				{ID: "repo-id-1", Name: "my-repo", IsDisabled: true},
			}, nil
		},
	}

	cmd := newDisableAdoRepoCmd(adoAPI, &mockDisableAdoRepoEnv{adoPAT: "ado-token"}, log)
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
	assert.Contains(t, output, "Repo 'my-ado-org/my-project/my-repo' is already disabled - No action will be performed")

	// Verify DisableRepo was NOT called
	assert.False(t, adoAPI.disableRepoCalled)
}
