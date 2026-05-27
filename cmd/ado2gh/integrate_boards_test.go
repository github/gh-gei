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
// Test constants
// ---------------------------------------------------------------------------

const (
	testBoardsTeamProjectID = "tp-id-123"
	testBoardsGithubHandle  = "octocat"
	testBoardsUUID          = "test-uuid"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockIntegrateBoardsAdoAPI struct {
	getTeamProjectIDFn           func(ctx context.Context, org, teamProject string) (string, error)
	getGithubHandleFn            func(ctx context.Context, org, teamProject, githubToken string) (string, error)
	getBoardsGithubConnectionFn  func(ctx context.Context, org, teamProject string) (ado.BoardsConnection, error)
	createBoardsGithubEndpointFn func(ctx context.Context, org, teamProjectId, githubToken, githubHandle, endpointName string) (string, error)
	getBoardsGithubRepoIDFn      func(ctx context.Context, org, teamProject, teamProjectId, endpointId, githubOrg, githubRepo string) (string, error)
	createBoardsGithubConnFn     func(ctx context.Context, org, teamProject, endpointId, repoId string) error
	addRepoToBoardsConnFn        func(ctx context.Context, org, teamProject, connectionId, connectionName, endpointId string, repoIds []string) error

	createBoardsGithubEndpointCalled bool
	getBoardsGithubRepoIDCalled      bool
	createBoardsGithubConnCalled     bool
	addRepoToBoardsConnCalled        bool
	addRepoToBoardsConnRepoIDs       []string
}

func (m *mockIntegrateBoardsAdoAPI) GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error) {
	return m.getTeamProjectIDFn(ctx, org, teamProject)
}

func (m *mockIntegrateBoardsAdoAPI) GetGithubHandle(ctx context.Context, org, teamProject, githubToken string) (string, error) {
	return m.getGithubHandleFn(ctx, org, teamProject, githubToken)
}

func (m *mockIntegrateBoardsAdoAPI) GetBoardsGithubConnection(ctx context.Context, org, teamProject string) (ado.BoardsConnection, error) {
	return m.getBoardsGithubConnectionFn(ctx, org, teamProject)
}

func (m *mockIntegrateBoardsAdoAPI) CreateBoardsGithubEndpoint(ctx context.Context, org, teamProjectId, githubToken, githubHandle, endpointName string) (string, error) {
	m.createBoardsGithubEndpointCalled = true
	return m.createBoardsGithubEndpointFn(ctx, org, teamProjectId, githubToken, githubHandle, endpointName)
}

func (m *mockIntegrateBoardsAdoAPI) GetBoardsGithubRepoId(ctx context.Context, org, teamProject, teamProjectId, endpointId, githubOrg, githubRepo string) (string, error) {
	m.getBoardsGithubRepoIDCalled = true
	return m.getBoardsGithubRepoIDFn(ctx, org, teamProject, teamProjectId, endpointId, githubOrg, githubRepo)
}

func (m *mockIntegrateBoardsAdoAPI) CreateBoardsGithubConnection(ctx context.Context, org, teamProject, endpointId, repoId string) error {
	m.createBoardsGithubConnCalled = true
	return m.createBoardsGithubConnFn(ctx, org, teamProject, endpointId, repoId)
}

func (m *mockIntegrateBoardsAdoAPI) AddRepoToBoardsGithubConnection(ctx context.Context, org, teamProject, connectionId, connectionName, endpointId string, repoIds []string) error {
	m.addRepoToBoardsConnCalled = true
	m.addRepoToBoardsConnRepoIDs = repoIds
	return m.addRepoToBoardsConnFn(ctx, org, teamProject, connectionId, connectionName, endpointId, repoIds)
}

type mockIntegrateBoardsEnvProvider struct {
	adoPAT    string
	githubPAT string
}

func (m *mockIntegrateBoardsEnvProvider) ADOPAT() string          { return m.adoPAT }
func (m *mockIntegrateBoardsEnvProvider) TargetGitHubPAT() string { return m.githubPAT }

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestIntegrateBoards_NoExistingConnection(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockIntegrateBoardsAdoAPI{
		getTeamProjectIDFn: func(_ context.Context, _, _ string) (string, error) {
			return testBoardsTeamProjectID, nil
		},
		getGithubHandleFn: func(_ context.Context, _, _, _ string) (string, error) {
			return testBoardsGithubHandle, nil
		},
		getBoardsGithubConnectionFn: func(_ context.Context, _, _ string) (ado.BoardsConnection, error) {
			return ado.BoardsConnection{}, nil // no existing connection
		},
		createBoardsGithubEndpointFn: func(_ context.Context, _, _, _, _, _ string) (string, error) {
			return "endpoint-id-456", nil
		},
		getBoardsGithubRepoIDFn: func(_ context.Context, _, _, _, _, _, _ string) (string, error) {
			return "repo-node-id-789", nil
		},
		createBoardsGithubConnFn: func(_ context.Context, _, _, _, _ string) error {
			return nil
		},
		addRepoToBoardsConnFn: func(_ context.Context, _, _, _, _, _ string, _ []string) error {
			t.Fatal("AddRepoToBoardsGithubConnection should not be called for new connection")
			return nil
		},
	}

	uuidFunc := func() string { return testBoardsUUID }

	cmd := newIntegrateBoardsCmd(adoAPI, &mockIntegrateBoardsEnvProvider{githubPAT: "gh-token"}, log, uuidFunc)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.True(t, adoAPI.createBoardsGithubEndpointCalled, "CreateBoardsGithubEndpoint should be called")
	assert.True(t, adoAPI.getBoardsGithubRepoIDCalled, "GetBoardsGithubRepoId should be called")
	assert.True(t, adoAPI.createBoardsGithubConnCalled, "CreateBoardsGithubConnection should be called")
	assert.False(t, adoAPI.addRepoToBoardsConnCalled, "AddRepoToBoardsGithubConnection should not be called")

	output := buf.String()
	assert.Contains(t, output, "Integrating Azure Boards...")
	assert.Contains(t, output, "Successfully configured Boards<->GitHub integration")
}

func TestIntegrateBoards_AddRepoToExistingConnection(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	existingConn := ado.BoardsConnection{
		ConnectionID:   "conn-id-100",
		EndpointID:     "endpoint-id-200",
		ConnectionName: "existing-conn",
		RepoIDs:        []string{"existing-repo-id-1"},
	}

	adoAPI := &mockIntegrateBoardsAdoAPI{
		getTeamProjectIDFn: func(_ context.Context, _, _ string) (string, error) {
			return testBoardsTeamProjectID, nil
		},
		getGithubHandleFn: func(_ context.Context, _, _, _ string) (string, error) {
			return testBoardsGithubHandle, nil
		},
		getBoardsGithubConnectionFn: func(_ context.Context, _, _ string) (ado.BoardsConnection, error) {
			return existingConn, nil
		},
		createBoardsGithubEndpointFn: func(_ context.Context, _, _, _, _, _ string) (string, error) {
			t.Fatal("CreateBoardsGithubEndpoint should not be called for existing connection")
			return "", nil
		},
		getBoardsGithubRepoIDFn: func(_ context.Context, _, _, _, _, _, _ string) (string, error) {
			return "new-repo-id-2", nil
		},
		createBoardsGithubConnFn: func(_ context.Context, _, _, _, _ string) error {
			t.Fatal("CreateBoardsGithubConnection should not be called for existing connection")
			return nil
		},
		addRepoToBoardsConnFn: func(_ context.Context, _, _, _, _, _ string, _ []string) error {
			return nil
		},
	}

	uuidFunc := func() string { return testBoardsUUID }

	cmd := newIntegrateBoardsCmd(adoAPI, &mockIntegrateBoardsEnvProvider{githubPAT: "gh-token"}, log, uuidFunc)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.False(t, adoAPI.createBoardsGithubEndpointCalled, "CreateBoardsGithubEndpoint should not be called")
	assert.True(t, adoAPI.addRepoToBoardsConnCalled, "AddRepoToBoardsGithubConnection should be called")
	assert.Equal(t, []string{"existing-repo-id-1", "new-repo-id-2"}, adoAPI.addRepoToBoardsConnRepoIDs)

	output := buf.String()
	assert.Contains(t, output, "Successfully configured Boards<->GitHub integration")
}

func TestIntegrateBoards_RepoAlreadyIntegrated(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	existingConn := ado.BoardsConnection{
		ConnectionID:   "conn-id-100",
		EndpointID:     "endpoint-id-200",
		ConnectionName: "existing-conn",
		RepoIDs:        []string{"already-integrated-repo-id"},
	}

	adoAPI := &mockIntegrateBoardsAdoAPI{
		getTeamProjectIDFn: func(_ context.Context, _, _ string) (string, error) {
			return testBoardsTeamProjectID, nil
		},
		getGithubHandleFn: func(_ context.Context, _, _, _ string) (string, error) {
			return testBoardsGithubHandle, nil
		},
		getBoardsGithubConnectionFn: func(_ context.Context, _, _ string) (ado.BoardsConnection, error) {
			return existingConn, nil
		},
		createBoardsGithubEndpointFn: func(_ context.Context, _, _, _, _, _ string) (string, error) {
			t.Fatal("CreateBoardsGithubEndpoint should not be called")
			return "", nil
		},
		getBoardsGithubRepoIDFn: func(_ context.Context, _, _, _, _, _, _ string) (string, error) {
			return "already-integrated-repo-id", nil // same as existing
		},
		createBoardsGithubConnFn: func(_ context.Context, _, _, _, _ string) error {
			t.Fatal("CreateBoardsGithubConnection should not be called")
			return nil
		},
		addRepoToBoardsConnFn: func(_ context.Context, _, _, _, _, _ string, _ []string) error {
			t.Fatal("AddRepoToBoardsGithubConnection should not be called when repo already integrated")
			return nil
		},
	}

	uuidFunc := func() string { return testBoardsUUID }

	cmd := newIntegrateBoardsCmd(adoAPI, &mockIntegrateBoardsEnvProvider{githubPAT: "gh-token"}, log, uuidFunc)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "target-org",
		"--github-repo", "target-repo",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.False(t, adoAPI.addRepoToBoardsConnCalled, "AddRepoToBoardsGithubConnection should not be called")

	output := buf.String()
	assert.Contains(t, output, "This repo is already configured in the Boards integration (Repo ID: already-integrated-repo-id)")
}

func TestIntegrateBoards_URLValidation(t *testing.T) {
	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name: "github-org is URL",
			args: []string{
				"--ado-org", "org", "--ado-team-project", "proj",
				"--github-org", "https://github.com/my-org",
				"--github-repo", "target-repo",
			},
			wantErr: "--github-org expects a name, not a URL",
		},
		{
			name: "github-repo is URL",
			args: []string{
				"--ado-org", "org", "--ado-team-project", "proj",
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

			adoAPI := &mockIntegrateBoardsAdoAPI{
				getTeamProjectIDFn: func(_ context.Context, _, _ string) (string, error) {
					return "", nil
				},
				getGithubHandleFn: func(_ context.Context, _, _, _ string) (string, error) {
					return "", nil
				},
				getBoardsGithubConnectionFn: func(_ context.Context, _, _ string) (ado.BoardsConnection, error) {
					return ado.BoardsConnection{}, nil
				},
				createBoardsGithubEndpointFn: func(_ context.Context, _, _, _, _, _ string) (string, error) {
					return "", nil
				},
				getBoardsGithubRepoIDFn: func(_ context.Context, _, _, _, _, _, _ string) (string, error) {
					return "", nil
				},
				createBoardsGithubConnFn: func(_ context.Context, _, _, _, _ string) error {
					return nil
				},
				addRepoToBoardsConnFn: func(_ context.Context, _, _, _, _, _ string, _ []string) error {
					return nil
				},
			}

			uuidFunc := func() string { return testBoardsUUID }

			cmd := newIntegrateBoardsCmd(adoAPI, &mockIntegrateBoardsEnvProvider{}, log, uuidFunc)
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()
			require.Error(t, err)
			assert.Contains(t, err.Error(), tc.wantErr)
		})
	}
}
