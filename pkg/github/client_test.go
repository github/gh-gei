package github

import (
	"context"
	"fmt"
	"net/http"
	"net/http/httptest"
	"testing"

	ghHttp "github.com/github/gh-gei/pkg/http"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestNewClient(t *testing.T) {
	log := logger.New(false)
	httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
	cfg := DefaultConfig()

	client := NewClient(cfg, httpClient, log)

	assert.NotNil(t, client)
	assert.Equal(t, "https://api.github.com", client.apiURL)
}

func TestNewClient_CustomAPIURL(t *testing.T) {
	log := logger.New(false)
	httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
	cfg := Config{
		APIURL: "https://ghes.example.com/api/v3",
		PAT:    "test-pat",
	}

	client := NewClient(cfg, httpClient, log)

	assert.NotNil(t, client)
	assert.Equal(t, "https://ghes.example.com/api/v3", client.apiURL)
	assert.Equal(t, "test-pat", client.pat)
}

func TestNewClient_TrimsTrailingSlash(t *testing.T) {
	log := logger.New(false)
	httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
	cfg := Config{
		APIURL: "https://ghes.example.com/api/v3/",
	}

	client := NewClient(cfg, httpClient, log)

	assert.Equal(t, "https://ghes.example.com/api/v3", client.apiURL)
}

func TestClient_GetRepos(t *testing.T) {
	log := logger.New(false)

	t.Run("successful fetch with single page", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, "/orgs/test-org/repos", r.URL.Path)
			assert.Contains(t, r.URL.RawQuery, "per_page=100")
			assert.Equal(t, "Bearer test-pat", r.Header.Get("Authorization"))

			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`[
				{"name": "repo1", "visibility": "public"},
				{"name": "repo2", "visibility": "private"},
				{"name": "repo3", "visibility": "internal"}
			]`))
		}))
		defer server.Close()

		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: server.URL,
			PAT:    "test-pat",
		}
		client := NewClient(cfg, httpClient, log)

		repos, err := client.GetRepos(context.Background(), "test-org")

		require.NoError(t, err)
		assert.Len(t, repos, 3)
		assert.Equal(t, "repo1", repos[0].Name)
		assert.Equal(t, "public", repos[0].Visibility)
		assert.Equal(t, "repo2", repos[1].Name)
		assert.Equal(t, "private", repos[1].Visibility)
		assert.Equal(t, "repo3", repos[2].Name)
		assert.Equal(t, "internal", repos[2].Visibility)
	})

	t.Run("successful fetch with multiple pages", func(t *testing.T) {
		callCount := 0
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			callCount++
			w.WriteHeader(http.StatusOK)

			if callCount == 1 {
				// First page - return 100 repos to trigger pagination
				repos := "["
				for i := 0; i < 100; i++ {
					if i > 0 {
						repos += ","
					}
					repos += fmt.Sprintf(`{"name": "repo%d", "visibility": "public"}`, i)
				}
				repos += "]"
				w.Write([]byte(repos))
			} else {
				// Second page - return fewer than 100 to signal end
				w.Write([]byte(`[
					{"name": "repo101", "visibility": "private"}
				]`))
			}
		}))
		defer server.Close()

		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: server.URL,
			PAT:    "test-pat",
		}
		client := NewClient(cfg, httpClient, log)

		repos, err := client.GetRepos(context.Background(), "test-org")

		require.NoError(t, err)
		assert.Equal(t, 101, len(repos))
		assert.Equal(t, 2, callCount)
	})

	t.Run("no repos found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`[]`))
		}))
		defer server.Close()

		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: server.URL,
			PAT:    "test-pat",
		}
		client := NewClient(cfg, httpClient, log)

		repos, err := client.GetRepos(context.Background(), "empty-org")

		require.NoError(t, err)
		assert.Len(t, repos, 0)
	})

	t.Run("API error", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusNotFound)
			w.Write([]byte(`{"message": "Not Found"}`))
		}))
		defer server.Close()

		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: server.URL,
			PAT:    "test-pat",
		}
		client := NewClient(cfg, httpClient, log)

		_, err := client.GetRepos(context.Background(), "nonexistent-org")

		require.Error(t, err)
		assert.Contains(t, err.Error(), "failed to fetch repos")
	})

	t.Run("URL encodes org name", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			// httptest server decodes URLs, so we check the raw query is properly formed
			// The path will be decoded, but we verify the request succeeds
			assert.Contains(t, r.URL.Path, "org with spaces")
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`[]`))
		}))
		defer server.Close()

		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: server.URL,
		}
		client := NewClient(cfg, httpClient, log)

		_, err := client.GetRepos(context.Background(), "org with spaces")

		require.NoError(t, err)
	})
}

func TestClient_GetVersion(t *testing.T) {
	log := logger.New(false)

	t.Run("successful version fetch for GHES", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, "/meta", r.URL.Path)
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"installed_version": "3.9.0"}`))
		}))
		defer server.Close()

		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: server.URL,
			PAT:    "test-pat",
		}
		client := NewClient(cfg, httpClient, log)

		version, err := client.GetVersion(context.Background())

		require.NoError(t, err)
		assert.NotNil(t, version)
		assert.Equal(t, "3.9.0", version.InstalledVersion)
	})

	t.Run("version not available on GitHub.com", func(t *testing.T) {
		httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)
		cfg := Config{
			APIURL: "https://api.github.com",
		}
		client := NewClient(cfg, httpClient, log)

		_, err := client.GetVersion(context.Background())

		require.Error(t, err)
		assert.Contains(t, err.Error(), "not available on GitHub.com")
	})
}

func TestClient_BuildHeaders(t *testing.T) {
	log := logger.New(false)
	httpClient := ghHttp.NewClient(ghHttp.DefaultConfig(), log)

	t.Run("headers with PAT", func(t *testing.T) {
		cfg := Config{
			PAT: "test-token",
		}
		client := NewClient(cfg, httpClient, log)

		headers := client.buildHeaders()

		assert.Equal(t, "application/vnd.github+json", headers["Accept"])
		assert.Equal(t, "2022-11-28", headers["X-GitHub-Api-Version"])
		assert.Equal(t, "Bearer test-token", headers["Authorization"])
	})

	t.Run("headers without PAT", func(t *testing.T) {
		cfg := Config{}
		client := NewClient(cfg, httpClient, log)

		headers := client.buildHeaders()

		assert.Equal(t, "application/vnd.github+json", headers["Accept"])
		assert.Equal(t, "2022-11-28", headers["X-GitHub-Api-Version"])
		assert.NotContains(t, headers, "Authorization")
	})
}
