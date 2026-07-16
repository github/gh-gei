package github

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestNewClient(t *testing.T) {
	client := NewClient("test-pat")

	assert.NotNil(t, client)
	assert.Equal(t, "https://api.github.com", client.apiURL)
	assert.NotNil(t, client.rest)
	assert.NotNil(t, client.graphql)
	assert.NotNil(t, client.logger) // should get a default logger
}

func TestNewClient_WithOptions(t *testing.T) {
	log := logger.New(true)
	client := NewClient("test-pat",
		WithAPIURL("https://ghes.example.com/api/v3"),
		WithLogger(log),
		WithVersion("2.0.0"),
	)

	assert.NotNil(t, client)
	assert.Equal(t, "https://ghes.example.com/api/v3", client.apiURL)
	assert.Equal(t, log, client.logger)
}

func TestNewClient_TrimsTrailingSlash(t *testing.T) {
	client := NewClient("test-pat",
		WithAPIURL("https://ghes.example.com/api/v3/"),
	)

	assert.Equal(t, "https://ghes.example.com/api/v3", client.apiURL)
}

func TestNewClient_DefaultsToGitHubDotCom(t *testing.T) {
	client := NewClient("test-pat")

	assert.Equal(t, "https://api.github.com", client.apiURL)
}

func TestClient_GetRepos(t *testing.T) {
	t.Run("successful fetch with single page", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			// go-github requests /api/v3/orgs/{org}/repos or /orgs/{org}/repos
			assert.Contains(t, r.URL.Path, "/orgs/test-org/repos")
			assert.Equal(t, "Bearer test-pat", r.Header.Get("Authorization"))

			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `[
				{"name": "repo1", "visibility": "public"},
				{"name": "repo2", "visibility": "private"},
				{"name": "repo3", "visibility": "internal"}
			]`)
		}))
		defer server.Close()

		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL(server.URL),
			WithLogger(log),
		)

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
		// Use a mux so the Link header can reference the server URL
		mux := http.NewServeMux()
		server := httptest.NewServer(mux)
		defer server.Close()

		mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
			callCount++
			w.Header().Set("Content-Type", "application/json")

			if callCount == 1 {
				repos := "["
				for i := 0; i < 30; i++ {
					if i > 0 {
						repos += ","
					}
					repos += fmt.Sprintf(`{"name": "repo%d", "visibility": "public"}`, i)
				}
				repos += "]"
				// Link header pointing to the next page on this same server
				w.Header().Set("Link", fmt.Sprintf(`<%s%s?page=2>; rel="next"`, server.URL, r.URL.Path))
				w.WriteHeader(http.StatusOK)
				fmt.Fprint(w, repos)
			} else {
				w.WriteHeader(http.StatusOK)
				fmt.Fprint(w, `[{"name": "repo-last", "visibility": "private"}]`)
			}
		})

		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL(server.URL),
			WithLogger(log),
		)

		repos, err := client.GetRepos(context.Background(), "test-org")

		require.NoError(t, err)
		assert.Equal(t, 31, len(repos))
		assert.Equal(t, 2, callCount)
	})

	t.Run("no repos found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `[]`)
		}))
		defer server.Close()

		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL(server.URL),
			WithLogger(log),
		)

		repos, err := client.GetRepos(context.Background(), "empty-org")

		require.NoError(t, err)
		assert.Len(t, repos, 0)
	})

	t.Run("API error", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusNotFound)
			fmt.Fprint(w, `{"message": "Not Found", "documentation_url": "https://docs.github.com"}`)
		}))
		defer server.Close()

		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL(server.URL),
			WithLogger(log),
		)

		_, err := client.GetRepos(context.Background(), "nonexistent-org")

		require.Error(t, err)
	})
}

func TestClient_GetVersion(t *testing.T) {
	t.Run("successful version fetch for GHES", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"installed_version": "3.9.0", "verifiable_password_authentication": true}`)
		}))
		defer server.Close()

		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL(server.URL),
			WithLogger(log),
		)

		version, err := client.GetVersion(context.Background())

		require.NoError(t, err)
		assert.NotNil(t, version)
		assert.Equal(t, "3.9.0", version.InstalledVersion)
	})

	t.Run("version not available on GitHub.com", func(t *testing.T) {
		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL("https://api.github.com"),
			WithLogger(log),
		)

		_, err := client.GetVersion(context.Background())

		require.Error(t, err)
		assert.Contains(t, err.Error(), "not available on GitHub.com")
	})
}

func TestClient_GraphQL(t *testing.T) {
	t.Run("GraphQL method delegates to graphqlClient", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			if r.URL.Path == "/graphql" {
				w.WriteHeader(http.StatusOK)
				fmt.Fprint(w, `{"data":{"organization":{"id":"org123"}}}`)
				return
			}
			w.WriteHeader(http.StatusNotFound)
		}))
		defer server.Close()

		log := logger.New(false)
		client := NewClient("test-pat",
			WithAPIURL(server.URL),
			WithLogger(log),
		)

		data, err := client.GraphQL(context.Background(), "query { organization(login: \"test\") { id } }", nil)

		require.NoError(t, err)
		assert.JSONEq(t, `{"organization":{"id":"org123"}}`, string(data))
	})
}

// ---------------------------------------------------------------------------
// Helper: create a test client with a server that handles GraphQL requests
// ---------------------------------------------------------------------------

func newGraphQLTestServer(t *testing.T, handler func(w http.ResponseWriter, body string)) *httptest.Server {
	t.Helper()
	return httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/graphql" {
			t.Errorf("unexpected path: %s", r.URL.Path)
			w.WriteHeader(http.StatusNotFound)
			return
		}
		assert.Equal(t, "Bearer test-pat", r.Header.Get("Authorization"))
		bodyBytes, _ := io.ReadAll(r.Body)
		w.Header().Set("Content-Type", "application/json")
		handler(w, string(bodyBytes))
	}))
}

func newTestClient(t *testing.T, server *httptest.Server) *Client {
	t.Helper()
	return NewClient("test-pat",
		WithAPIURL(server.URL),
		WithLogger(logger.New(false)),
	)
}

// ---------------------------------------------------------------------------
// Group 1: Organization/User queries (GraphQL-based)
// ---------------------------------------------------------------------------

func TestClient_GetOrganizationId(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "organization")
		fmt.Fprint(w, `{"data":{"organization":{"login":"test-org","id":"ORG_ID_123","name":"Test Org"}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.GetOrganizationId(context.Background(), "test-org")

	require.NoError(t, err)
	assert.Equal(t, "ORG_ID_123", id)
}

func TestClient_GetOrganizationDatabaseId(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "databaseId")
		fmt.Fprint(w, `{"data":{"organization":{"login":"test-org","databaseId":12345,"name":"Test Org"}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.GetOrganizationDatabaseId(context.Background(), "test-org")

	require.NoError(t, err)
	assert.Equal(t, "12345", id)
}

func TestClient_GetEnterpriseId(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "enterprise")
		fmt.Fprint(w, `{"data":{"enterprise":{"slug":"test-ent","id":"ENT_ID_456"}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.GetEnterpriseId(context.Background(), "test-ent")

	require.NoError(t, err)
	assert.Equal(t, "ENT_ID_456", id)
}

func TestClient_GetLoginName(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "viewer")
		fmt.Fprint(w, `{"data":{"viewer":{"login":"monalisa"}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	login, err := client.GetLoginName(context.Background())

	require.NoError(t, err)
	assert.Equal(t, "monalisa", login)
}

func TestClient_GetUserId(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "user")
		fmt.Fprint(w, `{"data":{"user":{"id":"USER_ID_789","name":"Mona Lisa"}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.GetUserId(context.Background(), "monalisa")

	require.NoError(t, err)
	assert.Equal(t, "USER_ID_789", id)
}

// ---------------------------------------------------------------------------
// Group 2: REST org methods
// ---------------------------------------------------------------------------

func TestClient_DoesOrgExist(t *testing.T) {
	t.Run("org exists", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"login":"test-org"}`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		exists, err := client.DoesOrgExist(context.Background(), "test-org")

		require.NoError(t, err)
		assert.True(t, exists)
	})

	t.Run("org not found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusNotFound)
			fmt.Fprint(w, `{"message":"Not Found"}`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		exists, err := client.DoesOrgExist(context.Background(), "no-such-org")

		require.NoError(t, err)
		assert.False(t, exists)
	})
}

func TestClient_GetOrgMembershipForUser(t *testing.T) {
	t.Run("member found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"role":"admin","state":"active","url":"https://api.github.com/orgs/test-org/memberships/test-user","organization_url":"https://api.github.com/orgs/test-org"}`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		role, err := client.GetOrgMembershipForUser(context.Background(), "test-org", "test-user")

		require.NoError(t, err)
		assert.Equal(t, "admin", role)
	})

	t.Run("not a member", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusNotFound)
			fmt.Fprint(w, `{"message":"Not Found"}`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		role, err := client.GetOrgMembershipForUser(context.Background(), "test-org", "not-a-member")

		require.NoError(t, err)
		assert.Equal(t, "", role)
	})
}

// ---------------------------------------------------------------------------
// Group 3: Migration mutations (GraphQL-based)
// ---------------------------------------------------------------------------

func TestClient_CreateAdoMigrationSource(t *testing.T) {
	t.Run("with ado server URL", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			assert.Contains(t, body, "AZURE_DEVOPS")
			assert.Contains(t, body, "https://ado.example.com")
			fmt.Fprint(w, `{"data":{"createMigrationSource":{"migrationSource":{"id":"MS_ADO_123","name":"Azure DevOps Source","url":"https://ado.example.com","type":"AZURE_DEVOPS"}}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		id, err := client.CreateAdoMigrationSource(context.Background(), "ORG_ID", "https://ado.example.com")

		require.NoError(t, err)
		assert.Equal(t, "MS_ADO_123", id)
	})

	t.Run("with empty ado server URL defaults to dev.azure.com", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			assert.Contains(t, body, "AZURE_DEVOPS")
			assert.Contains(t, body, "https://dev.azure.com")
			fmt.Fprint(w, `{"data":{"createMigrationSource":{"migrationSource":{"id":"MS_ADO_456","name":"Azure DevOps Source","url":"https://dev.azure.com","type":"AZURE_DEVOPS"}}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		id, err := client.CreateAdoMigrationSource(context.Background(), "ORG_ID", "")

		require.NoError(t, err)
		assert.Equal(t, "MS_ADO_456", id)
	})
}

func TestClient_CreateBbsMigrationSource(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "BITBUCKET_SERVER")
		fmt.Fprint(w, `{"data":{"createMigrationSource":{"migrationSource":{"id":"MS_BBS_123","name":"Bitbucket Server Source","url":"https://not-used","type":"BITBUCKET_SERVER"}}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.CreateBbsMigrationSource(context.Background(), "ORG_ID")

	require.NoError(t, err)
	assert.Equal(t, "MS_BBS_123", id)
}

func TestClient_CreateGhecMigrationSource(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "GITHUB_ARCHIVE")
		fmt.Fprint(w, `{"data":{"createMigrationSource":{"migrationSource":{"id":"MS_GHEC_123","name":"GHEC Source","url":"https://github.com","type":"GITHUB_ARCHIVE"}}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.CreateGhecMigrationSource(context.Background(), "ORG_ID")

	require.NoError(t, err)
	assert.Equal(t, "MS_GHEC_123", id)
}

func TestClient_StartMigration(t *testing.T) {
	t.Run("basic migration", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			assert.Contains(t, body, "startRepositoryMigration")
			assert.Contains(t, body, "SRC_ID")
			assert.Contains(t, body, "ORG_ID")
			assert.Contains(t, body, "my-repo")
			fmt.Fprint(w, `{"data":{"startRepositoryMigration":{"repositoryMigration":{"id":"MIG_123","databaseId":"999","migrationSource":{"id":"SRC_ID","name":"Test Source","type":"GITHUB_ARCHIVE"},"sourceUrl":"https://github.com/org/repo","state":"QUEUED","failureReason":""}}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		id, err := client.StartMigration(context.Background(), "SRC_ID", "https://github.com/org/repo", "ORG_ID", "my-repo", "source-token", "target-token")

		require.NoError(t, err)
		assert.Equal(t, "MIG_123", id)
	})

	t.Run("with options", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			// Parse the body to verify options were set
			var req struct {
				Variables map[string]interface{} `json:"variables"`
			}
			err := json.Unmarshal([]byte(body), &req)
			require.NoError(t, err)
			assert.Equal(t, true, req.Variables["skipReleases"])
			assert.Equal(t, true, req.Variables["lockSource"])
			assert.Equal(t, "private", req.Variables["targetRepoVisibility"])

			fmt.Fprint(w, `{"data":{"startRepositoryMigration":{"repositoryMigration":{"id":"MIG_456","databaseId":"998","migrationSource":{"id":"SRC_ID","name":"Test","type":"GITHUB_ARCHIVE"},"sourceUrl":"https://github.com/org/repo","state":"QUEUED","failureReason":""}}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		id, err := client.StartMigration(context.Background(),
			"SRC_ID", "https://github.com/org/repo", "ORG_ID", "my-repo", "src-tok", "tgt-tok",
			WithSkipReleases(true),
			WithLockSource(true),
			WithTargetRepoVisibility("private"),
		)

		require.NoError(t, err)
		assert.Equal(t, "MIG_456", id)
	})
}

func TestClient_StartBbsMigration(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		// Verify that StartBbsMigration delegates to StartMigration with proper params
		var req struct {
			Variables map[string]interface{} `json:"variables"`
		}
		err := json.Unmarshal([]byte(body), &req)
		require.NoError(t, err)
		assert.Equal(t, "not-used", req.Variables["accessToken"])
		assert.Equal(t, "https://archive.example.com/archive.tar.gz", req.Variables["gitArchiveUrl"])

		fmt.Fprint(w, `{"data":{"startRepositoryMigration":{"repositoryMigration":{"id":"MIG_BBS_123","databaseId":"997","migrationSource":{"id":"SRC_ID","name":"BBS Source","type":"BITBUCKET_SERVER"},"sourceUrl":"https://bbs.example.com/repo","state":"QUEUED","failureReason":""}}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.StartBbsMigration(context.Background(),
		"SRC_ID", "https://bbs.example.com/repo", "ORG_ID", "my-repo",
		"target-token", "https://archive.example.com/archive.tar.gz", "private",
	)

	require.NoError(t, err)
	assert.Equal(t, "MIG_BBS_123", id)
}

func TestClient_StartOrganizationMigration(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "startOrganizationMigration")
		assert.Contains(t, body, "https://github.com/source-org")
		assert.Contains(t, body, "target-org")
		fmt.Fprint(w, `{"data":{"startOrganizationMigration":{"orgMigration":{"id":"ORG_MIG_123","databaseId":"888"}}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	id, err := client.StartOrganizationMigration(context.Background(),
		"https://github.com/source-org", "target-org", "ENT_ID", "source-token",
	)

	require.NoError(t, err)
	assert.Equal(t, "ORG_MIG_123", id)
}

// ---------------------------------------------------------------------------
// Group 4: Migration queries (GraphQL-based)
// ---------------------------------------------------------------------------

func TestClient_GetMigration(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "node")
		fmt.Fprint(w, `{"data":{"node":{
			"id":"MIG_123",
			"sourceUrl":"https://github.com/org/repo",
			"migrationLogUrl":"https://example.com/log",
			"migrationSource":{"name":"GHEC Source"},
			"state":"SUCCEEDED",
			"warningsCount":5,
			"failureReason":"",
			"repositoryName":"my-repo"
		}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	mig, err := client.GetMigration(context.Background(), "MIG_123")

	require.NoError(t, err)
	assert.Equal(t, "MIG_123", mig.ID)
	assert.Equal(t, "https://github.com/org/repo", mig.SourceURL)
	assert.Equal(t, "https://example.com/log", mig.MigrationLogURL)
	assert.Equal(t, "GHEC Source", mig.MigrationSource.Name)
	assert.Equal(t, "SUCCEEDED", mig.State)
	assert.Equal(t, 5, mig.WarningsCount)
	assert.Equal(t, "", mig.FailureReason)
	assert.Equal(t, "my-repo", mig.RepositoryName)
}

func TestClient_GetOrganizationMigration(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		fmt.Fprint(w, `{"data":{"node":{
			"state":"IN_PROGRESS",
			"sourceOrgUrl":"https://github.com/source-org",
			"targetOrgName":"target-org",
			"failureReason":"",
			"remainingRepositoriesCount":3,
			"totalRepositoriesCount":10
		}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	mig, err := client.GetOrganizationMigration(context.Background(), "ORG_MIG_123")

	require.NoError(t, err)
	assert.Equal(t, "IN_PROGRESS", mig.State)
	assert.Equal(t, "https://github.com/source-org", mig.SourceOrgURL)
	assert.Equal(t, "target-org", mig.TargetOrgName)
	assert.Equal(t, "", mig.FailureReason)
	assert.Equal(t, 3, mig.RemainingRepositoriesCount)
	assert.Equal(t, 10, mig.TotalRepositoriesCount)
}

func TestClient_GetMigrationLogUrl(t *testing.T) {
	t.Run("found", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			fmt.Fprint(w, `{"data":{"organization":{"repositoryMigrations":{"nodes":[{"id":"MIG_123","migrationLogUrl":"https://example.com/log"}]}}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		result, err := client.GetMigrationLogUrl(context.Background(), "test-org", "my-repo")

		require.NoError(t, err)
		assert.Equal(t, "https://example.com/log", result.MigrationLogURL)
		assert.Equal(t, "MIG_123", result.MigrationID)
	})

	t.Run("not found", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			fmt.Fprint(w, `{"data":{"organization":{"repositoryMigrations":{"nodes":[]}}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		result, err := client.GetMigrationLogUrl(context.Background(), "test-org", "no-such-repo")

		require.NoError(t, err)
		assert.Equal(t, "", result.MigrationLogURL)
		assert.Equal(t, "", result.MigrationID)
	})
}

// ---------------------------------------------------------------------------
// Group 5: Abort and migrator role (GraphQL-based)
// ---------------------------------------------------------------------------

func TestClient_AbortMigration(t *testing.T) {
	t.Run("success", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			fmt.Fprint(w, `{"data":{"abortRepositoryMigration":{"success":true}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		success, err := client.AbortMigration(context.Background(), "MIG_123")

		require.NoError(t, err)
		assert.True(t, success)
	})

	t.Run("invalid migration id", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			fmt.Fprint(w, `{"errors":[{"message":"Could not resolve to a node with the global id of 'INVALID_ID'.","type":"NOT_FOUND"}]}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		_, err := client.AbortMigration(context.Background(), "INVALID_ID")

		require.Error(t, err)
		assert.Contains(t, err.Error(), "invalid migration id")
	})
}

func TestClient_GrantMigratorRole(t *testing.T) {
	t.Run("success", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			assert.Contains(t, body, "grantMigratorRole")
			fmt.Fprint(w, `{"data":{"grantMigratorRole":{"success":true}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		success, err := client.GrantMigratorRole(context.Background(), "ORG_ID", "monalisa", "USER")

		assert.NoError(t, err)
		assert.True(t, success)
	})

	t.Run("error returns false", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusInternalServerError)
			fmt.Fprint(w, `Internal Server Error`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		success, err := client.GrantMigratorRole(context.Background(), "ORG_ID", "monalisa", "USER")

		assert.NoError(t, err) // NOT an error — matches C# behavior
		assert.False(t, success)
	})
}

func TestClient_RevokeMigratorRole(t *testing.T) {
	t.Run("success", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			assert.Contains(t, body, "revokeMigratorRole")
			fmt.Fprint(w, `{"data":{"revokeMigratorRole":{"success":true}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		success, err := client.RevokeMigratorRole(context.Background(), "ORG_ID", "monalisa", "USER")

		assert.NoError(t, err)
		assert.True(t, success)
	})

	t.Run("error returns false", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusInternalServerError)
			fmt.Fprint(w, `Internal Server Error`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		success, err := client.RevokeMigratorRole(context.Background(), "ORG_ID", "monalisa", "USER")

		assert.NoError(t, err) // NOT an error — matches C# behavior
		assert.False(t, success)
	})
}

// ---------------------------------------------------------------------------
// Group 6: Team methods (REST-based)
// ---------------------------------------------------------------------------

func TestClient_CreateTeam(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.Contains(r.URL.Path, "/orgs/test-org/teams") && r.Method == http.MethodPost {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusCreated)
			fmt.Fprint(w, `{"id":42,"name":"my-team","slug":"my-team"}`)
			return
		}
		w.WriteHeader(http.StatusNotFound)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	team, err := client.CreateTeam(context.Background(), "test-org", "my-team")

	require.NoError(t, err)
	assert.Equal(t, "42", team.ID)
	assert.Equal(t, "my-team", team.Name)
	assert.Equal(t, "my-team", team.Slug)
}

func TestClient_GetTeams(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		fmt.Fprint(w, `[
			{"id":1,"name":"alpha","slug":"alpha"},
			{"id":2,"name":"beta","slug":"beta"},
			{"id":3,"name":"gamma","slug":"gamma"}
		]`)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	teams, err := client.GetTeams(context.Background(), "test-org")

	require.NoError(t, err)
	require.Len(t, teams, 3)
	assert.Equal(t, "alpha", teams[0].Name)
	assert.Equal(t, "beta", teams[1].Name)
	assert.Equal(t, "gamma", teams[2].Name)
}

func TestClient_GetTeamMembers(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		fmt.Fprint(w, `[
			{"login":"alice","id":1},
			{"login":"bob","id":2}
		]`)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	members, err := client.GetTeamMembers(context.Background(), "test-org", "my-team")

	require.NoError(t, err)
	require.Len(t, members, 2)
	assert.Equal(t, "alice", members[0])
	assert.Equal(t, "bob", members[1])
}

func TestClient_RemoveTeamMember(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Equal(t, http.MethodDelete, r.Method)
		w.WriteHeader(http.StatusNoContent)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	err := client.RemoveTeamMember(context.Background(), "test-org", "my-team", "alice")

	require.NoError(t, err)
}

func TestClient_GetTeamSlug(t *testing.T) {
	t.Run("found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `[
				{"id":1,"name":"Alpha Team","slug":"alpha-team"},
				{"id":2,"name":"Beta Team","slug":"beta-team"}
			]`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		// Case-insensitive match
		slug, err := client.GetTeamSlug(context.Background(), "test-org", "alpha team")

		require.NoError(t, err)
		assert.Equal(t, "alpha-team", slug)
	})

	t.Run("not found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `[{"id":1,"name":"Alpha Team","slug":"alpha-team"}]`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		_, err := client.GetTeamSlug(context.Background(), "test-org", "nonexistent-team")

		require.Error(t, err)
		assert.Contains(t, err.Error(), "not found")
	})
}

func TestClient_AddTeamSync(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Equal(t, "PATCH", r.Method)
		assert.Contains(t, r.URL.Path, "team-sync/group-mappings")

		body, _ := io.ReadAll(r.Body)
		assert.Contains(t, string(body), "group_id")
		assert.Contains(t, string(body), "Test Group")

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		fmt.Fprint(w, `{"groups":[{"group_id":"42","group_name":"Test Group","group_description":"A test group"}]}`)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	err := client.AddTeamSync(context.Background(), "test-org", "my-team", "42", "Test Group", "A test group")

	require.NoError(t, err)
}

func TestClient_AddTeamToRepo(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Equal(t, http.MethodPut, r.Method)
		w.WriteHeader(http.StatusNoContent)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	err := client.AddTeamToRepo(context.Background(), "test-org", "my-team", "my-repo", "push")

	require.NoError(t, err)
}

func TestClient_GetIdpGroupId(t *testing.T) {
	t.Run("found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Contains(t, r.URL.Path, "external-groups")
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"groups":[{"group_id":42,"group_name":"Test Group"}]}`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		groupID, err := client.GetIdpGroupId(context.Background(), "test-org", "Test Group")

		require.NoError(t, err)
		assert.Equal(t, 42, groupID)
	})

	t.Run("not found", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.Header().Set("Content-Type", "application/json")
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"groups":[]}`)
		}))
		defer server.Close()

		client := newTestClient(t, server)
		_, err := client.GetIdpGroupId(context.Background(), "test-org", "No Such Group")

		require.Error(t, err)
		assert.Contains(t, err.Error(), "not found")
	})
}

func TestClient_AddEmuGroupToTeam(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		assert.Equal(t, "PATCH", r.Method)
		assert.Contains(t, r.URL.Path, "external-groups")

		body, _ := io.ReadAll(r.Body)
		assert.Contains(t, string(body), "group_id")

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		fmt.Fprint(w, `{"group_id":42}`)
	}))
	defer server.Close()

	client := newTestClient(t, server)
	err := client.AddEmuGroupToTeam(context.Background(), "test-org", "my-team", 42)

	require.NoError(t, err)
}

// ---------------------------------------------------------------------------
// Group 7: Mannequin methods (GraphQL-based)
// ---------------------------------------------------------------------------

func TestClient_GetMannequins(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "mannequins")
		// Return a single page with hasNextPage=false
		fmt.Fprint(w, `{"data":{"node":{"mannequins":{
			"pageInfo":{"endCursor":"cursor1","hasNextPage":false},
			"nodes":[
				{"login":"mona","id":"MANN_1","claimant":null},
				{"login":"lisa","id":"MANN_2","claimant":{"login":"real-lisa","id":"USER_2"}}
			]
		}}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	mannequins, err := client.GetMannequins(context.Background(), "ORG_ID")

	require.NoError(t, err)
	require.Len(t, mannequins, 2)

	assert.Equal(t, "mona", mannequins[0].Login)
	assert.Equal(t, "MANN_1", mannequins[0].ID)
	assert.Nil(t, mannequins[0].MappedUser)

	assert.Equal(t, "lisa", mannequins[1].Login)
	assert.Equal(t, "MANN_2", mannequins[1].ID)
	require.NotNil(t, mannequins[1].MappedUser)
	assert.Equal(t, "real-lisa", mannequins[1].MappedUser.Login)
	assert.Equal(t, "USER_2", mannequins[1].MappedUser.ID)
}

func TestClient_GetMannequinsByLogin(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "mannequins")
		// Verify login variable is present in the body
		assert.Contains(t, body, "mona")
		fmt.Fprint(w, `{"data":{"node":{"mannequins":{
			"pageInfo":{"endCursor":"","hasNextPage":false},
			"nodes":[
				{"login":"mona","id":"MANN_1","claimant":null}
			]
		}}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	mannequins, err := client.GetMannequinsByLogin(context.Background(), "ORG_ID", "mona")

	require.NoError(t, err)
	require.Len(t, mannequins, 1)
	assert.Equal(t, "mona", mannequins[0].Login)
	assert.Equal(t, "MANN_1", mannequins[0].ID)
}

func TestClient_CreateAttributionInvitation(t *testing.T) {
	server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
		assert.Contains(t, body, "createAttributionInvitation")
		fmt.Fprint(w, `{"data":{"createAttributionInvitation":{
			"source":{"id":"MANN_1","login":"mona"},
			"target":{"id":"USER_1","login":"real-mona"}
		}}}`)
	})
	defer server.Close()

	client := newTestClient(t, server)
	result, err := client.CreateAttributionInvitation(context.Background(), "ORG_ID", "MANN_1", "USER_1")

	require.NoError(t, err)
	require.NotNil(t, result.Source)
	assert.Equal(t, "MANN_1", result.Source.ID)
	assert.Equal(t, "mona", result.Source.Login)
	require.NotNil(t, result.Target)
	assert.Equal(t, "USER_1", result.Target.ID)
	assert.Equal(t, "real-mona", result.Target.Login)
}

func TestClient_ReclaimMannequinSkipInvitation(t *testing.T) {
	t.Run("success", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			assert.Contains(t, body, "reattributeMannequinToUser")
			fmt.Fprint(w, `{"data":{"reattributeMannequinToUser":{
				"source":{"id":"MANN_1","login":"mona"},
				"target":{"id":"USER_1","login":"real-mona"}
			}}}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		result, err := client.ReclaimMannequinSkipInvitation(context.Background(), "ORG_ID", "MANN_1", "USER_1")

		require.NoError(t, err)
		require.NotNil(t, result.Source)
		assert.Equal(t, "MANN_1", result.Source.ID)
		require.NotNil(t, result.Target)
		assert.Equal(t, "USER_1", result.Target.ID)
		assert.Empty(t, result.Errors)
	})

	t.Run("mutation not available", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			fmt.Fprint(w, `{"errors":[{"message":"Field 'reattributeMannequinToUser' doesn't exist on type 'Mutation'"}]}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		_, err := client.ReclaimMannequinSkipInvitation(context.Background(), "ORG_ID", "MANN_1", "USER_1")

		require.Error(t, err)
		assert.Contains(t, err.Error(), "not available")
	})

	t.Run("target must be member", func(t *testing.T) {
		server := newGraphQLTestServer(t, func(w http.ResponseWriter, body string) {
			fmt.Fprint(w, `{"errors":[{"message":"Target must be a member of the organization"}]}`)
		})
		defer server.Close()

		client := newTestClient(t, server)
		result, err := client.ReclaimMannequinSkipInvitation(context.Background(), "ORG_ID", "MANN_1", "USER_1")

		// Should NOT return a Go error — returns result with Errors populated
		require.NoError(t, err)
		require.NotNil(t, result)
		require.Len(t, result.Errors, 1)
		assert.Contains(t, result.Errors[0].Message, "Target must be a member")
	})
}
