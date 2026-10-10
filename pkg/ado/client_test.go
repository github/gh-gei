package ado

import (
	"context"
	"encoding/base64"
	"fmt"
	"net/http"
	"net/http/httptest"
	"os"
	"testing"

	pkghttp "github.com/github/gh-gei/pkg/http"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestNewClient(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://dev.azure.com", "test-pat", log, nil)

	assert.NotNil(t, client)
	assert.Equal(t, "https://dev.azure.com", client.baseURL)
	assert.Equal(t, "test-pat", client.pat)
	assert.NotNil(t, client.httpClient)
}

func TestNewClient_RemovesTrailingSlash(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://dev.azure.com/", "test-pat", log, nil)

	assert.Equal(t, "https://dev.azure.com", client.baseURL)
}

func TestGetTeamProjects_Success(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/ado/projects.json")
	require.NoError(t, err)

	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Verify request
		assert.Equal(t, "/test-org/_apis/projects", r.URL.Path)
		assert.Equal(t, "api-version=6.1-preview", r.URL.RawQuery)
		assert.Equal(t, "GET", r.Method)
		assert.Contains(t, r.Header.Get("Authorization"), "Basic")

		w.WriteHeader(http.StatusOK)
		w.Write(data)
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	// Execute
	projects, err := client.GetTeamProjects(context.Background(), "test-org")

	// Assert
	require.NoError(t, err)
	assert.Len(t, projects, 3)
	assert.Equal(t, "project-123", projects[0].ID)
	assert.Equal(t, "TestProject1", projects[0].Name)
	assert.Equal(t, "TestProject2", projects[1].Name)
	assert.Equal(t, "TestProject3", projects[2].Name)
}

func TestGetTeamProjects_EmptyOrg(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://dev.azure.com", "test-pat", log, nil)

	projects, err := client.GetTeamProjects(context.Background(), "")

	assert.Error(t, err)
	assert.Nil(t, projects)
	assert.Contains(t, err.Error(), "org cannot be empty")
}

func TestGetTeamProjects_URLEncoding(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Note: httptest.Server automatically decodes the URL path
		// So "/test%20org%20with%20spaces" becomes "/test org with spaces"
		assert.Equal(t, "/test org with spaces/_apis/projects", r.URL.Path)
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(`{"value": []}`))
	}))
	defer server.Close()

	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	_, err := client.GetTeamProjects(context.Background(), "test org with spaces")
	assert.NoError(t, err)
}

func TestGetRepos_Success(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/ado/repos.json")
	require.NoError(t, err)

	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Equal(t, "/test-org/test-project/_apis/git/repositories", r.URL.Path)
		assert.Equal(t, "api-version=6.1-preview.1", r.URL.RawQuery)
		assert.Equal(t, "GET", r.Method)

		w.WriteHeader(http.StatusOK)
		w.Write(data)
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	// Execute
	repos, err := client.GetRepos(context.Background(), "test-org", "test-project")

	// Assert
	require.NoError(t, err)
	assert.Len(t, repos, 3)
	assert.Equal(t, "repo-111", repos[0].ID)
	assert.Equal(t, "TestRepo1", repos[0].Name)
	assert.Equal(t, uint64(1024), repos[0].Size)
	assert.False(t, repos[0].IsDisabled)

	assert.Equal(t, "DisabledRepo", repos[2].Name)
	assert.True(t, repos[2].IsDisabled)
}

func TestGetRepos_EmptyParameters(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://dev.azure.com", "test-pat", log, nil)

	tests := []struct {
		name        string
		org         string
		teamProject string
		expectedErr string
	}{
		{"empty org", "", "project", "org cannot be empty"},
		{"empty project", "org", "", "teamProject cannot be empty"},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			repos, err := client.GetRepos(context.Background(), tt.org, tt.teamProject)
			assert.Error(t, err)
			assert.Nil(t, repos)
			assert.Contains(t, err.Error(), tt.expectedErr)
		})
	}
}

func TestGetEnabledRepos_Success(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/ado/repos.json")
	require.NoError(t, err)

	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write(data)
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	// Execute
	repos, err := client.GetEnabledRepos(context.Background(), "test-org", "test-project")

	// Assert
	require.NoError(t, err)
	assert.Len(t, repos, 2) // Only 2 enabled repos (DisabledRepo is filtered out)
	assert.Equal(t, "TestRepo1", repos[0].Name)
	assert.Equal(t, "TestRepo2", repos[1].Name)
	assert.False(t, repos[0].IsDisabled)
	assert.False(t, repos[1].IsDisabled)
}

func TestGetGithubAppId_Success(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/ado/service_endpoints.json")
	require.NoError(t, err)

	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Contains(t, r.URL.Path, "/_apis/serviceendpoint/endpoints")
		w.WriteHeader(http.StatusOK)
		w.Write(data)
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	// Execute - looking for GitHub endpoint
	appID, err := client.GetGithubAppId(context.Background(), "test-org", "test-github-org", []string{"TestProject1", "TestProject2"})

	// Assert
	require.NoError(t, err)
	assert.Equal(t, "endpoint-111", appID) // Should find the GitHub type endpoint
}

func TestGetGithubAppId_GitHubProximaPipelines(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/ado/service_endpoints.json")
	require.NoError(t, err)

	// Create mock server that returns no GitHub endpoint on first call, but GitHubProximaPipelines on second
	callCount := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		callCount++
		if callCount == 1 {
			// First project has no matching endpoint
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"value": []}`))
		} else {
			// Second project has GitHubProximaPipelines endpoint
			w.WriteHeader(http.StatusOK)
			w.Write(data)
		}
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	// Execute - looking for non-existent GitHub org, should find GitHubProximaPipelines instead
	appID, err := client.GetGithubAppId(context.Background(), "test-org", "nonexistent-org", []string{"Project0", "TestProject1"})

	// Assert
	require.NoError(t, err)
	assert.Equal(t, "endpoint-222", appID) // Should find the GitHubProximaPipelines endpoint
}

func TestGetGithubAppId_NotFound(t *testing.T) {
	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(`{"value": []}`)) // Empty response
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, encodePAT("test-pat"), log, httpClient)

	// Execute
	appID, err := client.GetGithubAppId(context.Background(), "test-org", "nonexistent-org", []string{"TestProject1"})

	// Assert
	require.NoError(t, err)
	assert.Empty(t, appID)
}

func TestGetGithubAppId_EmptyParameters(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://dev.azure.com", "test-pat", log, nil)

	tests := []struct {
		name         string
		org          string
		githubOrg    string
		teamProjects []string
		expectedErr  string
		expectEmpty  bool
	}{
		{"empty org", "", "github-org", []string{"project"}, "org cannot be empty", false},
		{"empty github org", "org", "", []string{"project"}, "githubOrg cannot be empty", false},
		{"empty projects", "org", "github-org", []string{}, "", true},
		{"nil projects", "org", "github-org", nil, "", true},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			appID, err := client.GetGithubAppId(context.Background(), tt.org, tt.githubOrg, tt.teamProjects)
			if tt.expectEmpty {
				assert.NoError(t, err)
				assert.Empty(t, appID)
			} else {
				assert.Error(t, err)
				assert.Contains(t, err.Error(), tt.expectedErr)
			}
		})
	}
}

func TestMakeAuthHeaders(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://dev.azure.com", "test-pat-token", log, nil)

	headers := client.makeAuthHeaders()

	assert.Equal(t, "Basic test-pat-token", headers["Authorization"])
	assert.Equal(t, "application/json", headers["Content-Type"])
}

// encodePAT mimics the base64 encoding that ADO expects for PAT tokens
func encodePAT(pat string) string {
	// ADO uses ":{PAT}" format encoded in base64
	return base64.StdEncoding.EncodeToString([]byte(fmt.Sprintf(":%s", pat)))
}
