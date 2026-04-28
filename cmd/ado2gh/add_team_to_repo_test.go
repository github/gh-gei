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

type mockAddTeamToRepoGitHub struct {
	getTeamSlugFn   func(ctx context.Context, org, teamName string) (string, error)
	addTeamToRepoFn func(ctx context.Context, org, teamSlug, repo, role string) error
}

func (m *mockAddTeamToRepoGitHub) GetTeamSlug(ctx context.Context, org, teamName string) (string, error) {
	return m.getTeamSlugFn(ctx, org, teamName)
}

func (m *mockAddTeamToRepoGitHub) AddTeamToRepo(ctx context.Context, org, teamSlug, repo, role string) error {
	return m.addTeamToRepoFn(ctx, org, teamSlug, repo, role)
}

type mockAddTeamToRepoEnv struct {
	targetPAT string
}

func (m *mockAddTeamToRepoEnv) TargetGitHubPAT() string { return m.targetPAT }

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestAddTeamToRepo_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	var capturedOrg, capturedTeamSlug, capturedRepo, capturedRole string

	gh := &mockAddTeamToRepoGitHub{
		getTeamSlugFn: func(_ context.Context, org, teamName string) (string, error) {
			assert.Equal(t, "my-org", org)
			assert.Equal(t, "my-team", teamName)
			return "foo-slug", nil
		},
		addTeamToRepoFn: func(_ context.Context, org, teamSlug, repo, role string) error {
			capturedOrg = org
			capturedTeamSlug = teamSlug
			capturedRepo = repo
			capturedRole = role
			return nil
		},
	}

	cmd := newAddTeamToRepoCmd(gh, &mockAddTeamToRepoEnv{targetPAT: "gh-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-org", "my-org",
		"--github-repo", "my-repo",
		"--team", "my-team",
		"--role", "push",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, "my-org", capturedOrg)
	assert.Equal(t, "foo-slug", capturedTeamSlug)
	assert.Equal(t, "my-repo", capturedRepo)
	assert.Equal(t, "push", capturedRole)

	output := buf.String()
	assert.Contains(t, output, "Adding team to repo...")
	assert.Contains(t, output, "Successfully added team to repo")
}
