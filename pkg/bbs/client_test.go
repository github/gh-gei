package bbs

import (
	"context"
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
	client := NewClient("https://bitbucket.example.com", "testuser", "testpass", log, nil)

	assert.NotNil(t, client)
	assert.Equal(t, "https://bitbucket.example.com", client.baseURL)
	assert.Equal(t, "testuser", client.username)
	assert.Equal(t, "testpass", client.password)
	assert.NotNil(t, client.httpClient)
}

func TestNewClient_RemovesTrailingSlash(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://bitbucket.example.com/", "testuser", "testpass", log, nil)

	assert.Equal(t, "https://bitbucket.example.com", client.baseURL)
}

func TestGetProjects_Success(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/bbs/projects.json")
	require.NoError(t, err)

	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Verify request
		assert.Equal(t, "/rest/api/1.0/projects", r.URL.Path)
		assert.Contains(t, r.URL.RawQuery, "start=0")
		assert.Contains(t, r.URL.RawQuery, "limit=25")
		assert.Equal(t, "GET", r.Method)
		assert.Contains(t, r.Header.Get("Authorization"), "Basic")

		w.WriteHeader(http.StatusOK)
		w.Write(data)
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, "testuser", "testpass", log, httpClient)

	// Execute
	projects, err := client.GetProjects(context.Background())

	// Assert
	require.NoError(t, err)
	assert.Len(t, projects, 2)
	assert.Equal(t, 1, projects[0].ID)
	assert.Equal(t, "PROJ1", projects[0].Key)
	assert.Equal(t, "Test Project 1", projects[0].Name)
	assert.Equal(t, "PROJ2", projects[1].Key)
}

func TestGetProjects_Pagination(t *testing.T) {
	callCount := 0

	// Create mock server that returns paginated data
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		callCount++

		if callCount == 1 {
			// First page
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{
				"values": [{"id": 1, "key": "PROJ1", "name": "Project 1"}],
				"size": 1,
				"isLastPage": false,
				"start": 0,
				"limit": 1,
				"nextPageStart": 1
			}`))
		} else {
			// Second page (last)
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{
				"values": [{"id": 2, "key": "PROJ2", "name": "Project 2"}],
				"size": 1,
				"isLastPage": true,
				"start": 1,
				"limit": 1
			}`))
		}
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, "testuser", "testpass", log, httpClient)

	// Execute
	projects, err := client.GetProjects(context.Background())

	// Assert
	require.NoError(t, err)
	assert.Len(t, projects, 2)
	assert.Equal(t, "PROJ1", projects[0].Key)
	assert.Equal(t, "PROJ2", projects[1].Key)
	assert.Equal(t, 2, callCount, "Should make 2 API calls for pagination")
}

func TestGetRepos_Success(t *testing.T) {
	// Read test data
	data, err := os.ReadFile("../../testdata/bbs/repos.json")
	require.NoError(t, err)

	// Create mock server
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Equal(t, "/rest/api/1.0/projects/PROJ1/repos", r.URL.Path)
		assert.Contains(t, r.URL.RawQuery, "start=0")
		assert.Contains(t, r.URL.RawQuery, "limit=25")
		assert.Equal(t, "GET", r.Method)

		w.WriteHeader(http.StatusOK)
		w.Write(data)
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, "testuser", "testpass", log, httpClient)

	// Execute
	repos, err := client.GetRepos(context.Background(), "PROJ1")

	// Assert
	require.NoError(t, err)
	assert.Len(t, repos, 3)
	assert.Equal(t, 101, repos[0].ID)
	assert.Equal(t, "repo-one", repos[0].Slug)
	assert.Equal(t, "Repository One", repos[0].Name)
	assert.Equal(t, "repo-two", repos[1].Slug)
	assert.Equal(t, "repo-three", repos[2].Slug)
}

func TestGetRepos_EmptyProjectKey(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://bitbucket.example.com", "testuser", "testpass", log, nil)

	repos, err := client.GetRepos(context.Background(), "")

	assert.Error(t, err)
	assert.Nil(t, repos)
	assert.Contains(t, err.Error(), "projectKey cannot be empty")
}

func TestGetRepos_URLEncoding(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Note: httptest.Server automatically decodes the URL path
		assert.Equal(t, "/rest/api/1.0/projects/PROJ WITH SPACES/repos", r.URL.Path)
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(`{"values": [], "size": 0, "isLastPage": true, "start": 0, "limit": 25}`))
	}))
	defer server.Close()

	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, "testuser", "testpass", log, httpClient)

	_, err := client.GetRepos(context.Background(), "PROJ WITH SPACES")
	assert.NoError(t, err)
}

func TestGetRepos_Pagination(t *testing.T) {
	// Read test data for pagination
	page1Data, err := os.ReadFile("../../testdata/bbs/repos_page1.json")
	require.NoError(t, err)
	page2Data, err := os.ReadFile("../../testdata/bbs/repos_page2.json")
	require.NoError(t, err)

	callCount := 0

	// Create mock server that returns paginated data
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		callCount++

		if r.URL.Query().Get("start") == "0" {
			// First page
			w.WriteHeader(http.StatusOK)
			w.Write(page1Data)
		} else {
			// Second page (last)
			w.WriteHeader(http.StatusOK)
			w.Write(page2Data)
		}
	}))
	defer server.Close()

	// Create client
	log := logger.New(false)
	httpClient := pkghttp.NewClient(pkghttp.DefaultConfig(), log)
	client := NewClient(server.URL, "testuser", "testpass", log, httpClient)

	// Execute
	repos, err := client.GetRepos(context.Background(), "PROJ1")

	// Assert
	require.NoError(t, err)
	assert.Len(t, repos, 2)
	assert.Equal(t, "repo-one", repos[0].Slug)
	assert.Equal(t, "repo-two", repos[1].Slug)
	assert.Equal(t, 2, callCount, "Should make 2 API calls for pagination")
}

func TestMakeAuthHeaders(t *testing.T) {
	log := logger.New(false)
	client := NewClient("https://bitbucket.example.com", "testuser", "testpass", log, nil)

	headers := client.makeAuthHeaders()

	assert.Equal(t, "Basic testuser:testpass", headers["Authorization"])
	assert.Equal(t, "application/json", headers["Content-Type"])
}
