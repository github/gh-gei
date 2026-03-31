package main

import (
	"bytes"
	"context"
	"testing"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockConfigureAutolinkGitHub struct {
	getAutoLinksFn   func(ctx context.Context, org, repo string) ([]github.AutoLink, error)
	addAutoLinkFn    func(ctx context.Context, org, repo, keyPrefix, urlTemplate string) error
	deleteAutoLinkFn func(ctx context.Context, org, repo string, autoLinkID int) error
}

func (m *mockConfigureAutolinkGitHub) GetAutoLinks(ctx context.Context, org, repo string) ([]github.AutoLink, error) {
	return m.getAutoLinksFn(ctx, org, repo)
}

func (m *mockConfigureAutolinkGitHub) AddAutoLink(ctx context.Context, org, repo, keyPrefix, urlTemplate string) error {
	return m.addAutoLinkFn(ctx, org, repo, keyPrefix, urlTemplate)
}

func (m *mockConfigureAutolinkGitHub) DeleteAutoLink(ctx context.Context, org, repo string, autoLinkID int) error {
	return m.deleteAutoLinkFn(ctx, org, repo, autoLinkID)
}

type mockConfigureAutolinkEnv struct {
	targetPAT string
}

func (m *mockConfigureAutolinkEnv) TargetGitHubPAT() string { return m.targetPAT }

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestConfigureAutolink_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	deleteCalled := false
	var capturedKeyPrefix, capturedURLTemplate string

	gh := &mockConfigureAutolinkGitHub{
		getAutoLinksFn: func(_ context.Context, _, _ string) ([]github.AutoLink, error) {
			return []github.AutoLink{}, nil // no existing autolinks
		},
		addAutoLinkFn: func(_ context.Context, _, _, keyPrefix, urlTemplate string) error {
			capturedKeyPrefix = keyPrefix
			capturedURLTemplate = urlTemplate
			return nil
		},
		deleteAutoLinkFn: func(_ context.Context, _, _ string, _ int) error {
			deleteCalled = true
			return nil
		},
	}

	cmd := newConfigureAutolinkCmd(gh, &mockConfigureAutolinkEnv{targetPAT: "gh-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-org", "my-org",
		"--github-repo", "my-repo",
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.False(t, deleteCalled, "DeleteAutoLink should not be called when no existing autolinks")
	assert.Equal(t, "AB#", capturedKeyPrefix)
	assert.Equal(t, "https://dev.azure.com/my-ado-org/my-project/_workitems/edit/<num>/", capturedURLTemplate)

	output := buf.String()
	assert.Contains(t, output, "Configuring Autolink Reference...")
	assert.Contains(t, output, "Successfully configured autolink references")
}

func TestConfigureAutolink_Idempotency_AutoLinkExists(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	addCalled := false
	deleteCalled := false

	gh := &mockConfigureAutolinkGitHub{
		getAutoLinksFn: func(_ context.Context, _, _ string) ([]github.AutoLink, error) {
			return []github.AutoLink{
				{
					ID:          1,
					KeyPrefix:   "AB#",
					URLTemplate: "https://dev.azure.com/my-ado-org/my-project/_workitems/edit/<num>/",
				},
			}, nil
		},
		addAutoLinkFn: func(_ context.Context, _, _, _, _ string) error {
			addCalled = true
			return nil
		},
		deleteAutoLinkFn: func(_ context.Context, _, _ string, _ int) error {
			deleteCalled = true
			return nil
		},
	}

	cmd := newConfigureAutolinkCmd(gh, &mockConfigureAutolinkEnv{targetPAT: "gh-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-org", "my-org",
		"--github-repo", "my-repo",
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.False(t, deleteCalled, "DeleteAutoLink should not be called when autolink already correct")
	assert.False(t, addCalled, "AddAutoLink should not be called when autolink already correct")

	output := buf.String()
	assert.Contains(t, output, "Autolink reference already exists for key_prefix: 'AB#'. No operation will be performed")
}

func TestConfigureAutolink_Idempotency_KeyPrefixExists_WrongTemplate(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	var deletedID int
	var capturedKeyPrefix, capturedURLTemplate string

	gh := &mockConfigureAutolinkGitHub{
		getAutoLinksFn: func(_ context.Context, _, _ string) ([]github.AutoLink, error) {
			return []github.AutoLink{
				{
					ID:          42,
					KeyPrefix:   "AB#",
					URLTemplate: "https://wrong-url.com/edit/<num>/",
				},
			}, nil
		},
		addAutoLinkFn: func(_ context.Context, _, _, keyPrefix, urlTemplate string) error {
			capturedKeyPrefix = keyPrefix
			capturedURLTemplate = urlTemplate
			return nil
		},
		deleteAutoLinkFn: func(_ context.Context, _, _ string, autoLinkID int) error {
			deletedID = autoLinkID
			return nil
		},
	}

	cmd := newConfigureAutolinkCmd(gh, &mockConfigureAutolinkEnv{targetPAT: "gh-token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--github-org", "my-org",
		"--github-repo", "my-repo",
		"--ado-org", "my-ado-org",
		"--ado-team-project", "my-project",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, 42, deletedID, "Should delete the existing autolink with wrong template")
	assert.Equal(t, "AB#", capturedKeyPrefix)
	assert.Equal(t, "https://dev.azure.com/my-ado-org/my-project/_workitems/edit/<num>/", capturedURLTemplate)

	output := buf.String()
	assert.Contains(t, output, "Autolink reference already exists for key_prefix: 'AB#', but the url template is incorrect")
	assert.Contains(t, output, "Deleting existing Autolink reference")
	assert.Contains(t, output, "Successfully configured autolink references")
}
