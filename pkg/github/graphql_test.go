package github

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"sync/atomic"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestGraphQLClient_Post(t *testing.T) {
	t.Run("sends correct headers and body", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			// Verify method and path
			assert.Equal(t, http.MethodPost, r.Method)
			assert.Equal(t, "/graphql", r.URL.Path)

			// Verify headers
			assert.Equal(t, "Bearer test-pat", r.Header.Get("Authorization"))
			assert.Equal(t, "import_api,mannequin_claiming_emu,org_import_api", r.Header.Get("GraphQL-Features"))
			assert.Contains(t, r.Header.Get("User-Agent"), "OctoshiftCLI/")
			assert.Equal(t, "application/json", r.Header.Get("Content-Type"))

			// Verify body
			body, err := io.ReadAll(r.Body)
			require.NoError(t, err)

			var req graphqlRequest
			err = json.Unmarshal(body, &req)
			require.NoError(t, err)
			assert.Equal(t, "query { viewer { login } }", req.Query)
			assert.Equal(t, json.RawMessage(`{"org":"test-org"}`), req.Variables)

			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"data":{"viewer":{"login":"testuser"}}}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		vars := json.RawMessage(`{"org":"test-org"}`)
		data, err := gql.Post(context.Background(), "query { viewer { login } }", vars)

		require.NoError(t, err)
		assert.JSONEq(t, `{"viewer":{"login":"testuser"}}`, string(data))
	})

	t.Run("returns GraphQL error from errors array", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"errors":[{"message":"Could not resolve to an Organization","type":"NOT_FOUND"}]}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		_, err := gql.Post(context.Background(), "query { organization(login: \"nope\") { id } }", nil)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "Could not resolve to an Organization")
	})

	t.Run("returns error on HTTP failure", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusInternalServerError)
			fmt.Fprint(w, `Internal Server Error`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		_, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "500")
	})

	t.Run("returns error when data field is null with errors", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"data":null,"errors":[{"message":"some error"}]}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		_, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "some error")
	})
}

func TestGraphQLClient_PostWithPagination(t *testing.T) {
	t.Run("collects results from two pages", func(t *testing.T) {
		var callCount atomic.Int32
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			body, _ := io.ReadAll(r.Body)
			var req graphqlRequest
			json.Unmarshal(body, &req)

			call := callCount.Add(1)

			var vars map[string]interface{}
			json.Unmarshal(req.Variables, &vars)

			// Verify pagination variables
			assert.Equal(t, float64(100), vars["first"])

			w.WriteHeader(http.StatusOK)
			if call == 1 {
				assert.Nil(t, vars["after"], "first call should not have after cursor")
				fmt.Fprint(w, `{"data":{"organization":{"repositories":{"nodes":[{"name":"repo1"},{"name":"repo2"}],"pageInfo":{"hasNextPage":true,"endCursor":"cursor123"}}}}}`)
			} else {
				assert.Equal(t, "cursor123", vars["after"])
				fmt.Fprint(w, `{"data":{"organization":{"repositories":{"nodes":[{"name":"repo3"}],"pageInfo":{"hasNextPage":false,"endCursor":""}}}}}`)
			}
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		query := `query($org: String!, $first: Int!, $after: String) {
			organization(login: $org) {
				repositories(first: $first, after: $after) {
					nodes { name }
					pageInfo { hasNextPage endCursor }
				}
			}
		}`
		vars := json.RawMessage(`{"org":"test-org"}`)

		results, err := gql.PostWithPagination(
			context.Background(),
			query,
			vars,
			"organization.repositories.nodes",
			"organization.repositories.pageInfo",
		)

		require.NoError(t, err)
		assert.Equal(t, int32(2), callCount.Load())

		// Results should contain 3 items combined from both pages
		var items []map[string]string
		err = json.Unmarshal(results, &items)
		require.NoError(t, err)
		assert.Len(t, items, 3)
		assert.Equal(t, "repo1", items[0]["name"])
		assert.Equal(t, "repo2", items[1]["name"])
		assert.Equal(t, "repo3", items[2]["name"])
	})

	t.Run("single page with no next page", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"data":{"organization":{"repositories":{"nodes":[{"name":"only-repo"}],"pageInfo":{"hasNextPage":false,"endCursor":""}}}}}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		results, err := gql.PostWithPagination(
			context.Background(),
			"query { ... }",
			json.RawMessage(`{"org":"test-org"}`),
			"organization.repositories.nodes",
			"organization.repositories.pageInfo",
		)

		require.NoError(t, err)
		var items []map[string]string
		err = json.Unmarshal(results, &items)
		require.NoError(t, err)
		assert.Len(t, items, 1)
		assert.Equal(t, "only-repo", items[0]["name"])
	})

	t.Run("returns error on GraphQL error during pagination", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"errors":[{"message":"rate limit exceeded"}]}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		_, err := gql.PostWithPagination(
			context.Background(),
			"query { ... }",
			nil,
			"organization.repositories.nodes",
			"organization.repositories.pageInfo",
		)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "rate limit exceeded")
	})
}

func TestGraphQLClient_SecondaryRateLimit(t *testing.T) {
	t.Run("retries on 429 with Retry-After header", func(t *testing.T) {
		var callCount atomic.Int32
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			call := callCount.Add(1)
			if call == 1 {
				w.Header().Set("Retry-After", "0") // 0 seconds for fast test
				w.WriteHeader(http.StatusTooManyRequests)
				fmt.Fprint(w, `{"message":"secondary rate limit"}`)
				return
			}
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"data":{"viewer":{"login":"testuser"}}}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		data, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.NoError(t, err)
		assert.JSONEq(t, `{"viewer":{"login":"testuser"}}`, string(data))
		assert.Equal(t, int32(2), callCount.Load())
	})

	t.Run("retries on 403 with SECONDARY RATE LIMIT in body", func(t *testing.T) {
		var callCount atomic.Int32
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			call := callCount.Add(1)
			if call == 1 {
				w.Header().Set("Retry-After", "0")
				w.WriteHeader(http.StatusForbidden)
				fmt.Fprint(w, `{"message":"You have exceeded a SECONDARY RATE LIMIT"}`)
				return
			}
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"data":{"viewer":{"login":"testuser"}}}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		data, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.NoError(t, err)
		assert.JSONEq(t, `{"viewer":{"login":"testuser"}}`, string(data))
		assert.Equal(t, int32(2), callCount.Load())
	})

	t.Run("retries on 403 with ABUSE DETECTION in body", func(t *testing.T) {
		var callCount atomic.Int32
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			call := callCount.Add(1)
			if call == 1 {
				w.Header().Set("Retry-After", "0")
				w.WriteHeader(http.StatusForbidden)
				fmt.Fprint(w, `{"message":"ABUSE DETECTION triggered"}`)
				return
			}
			w.WriteHeader(http.StatusOK)
			fmt.Fprint(w, `{"data":{"viewer":{"login":"testuser"}}}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		data, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.NoError(t, err)
		assert.JSONEq(t, `{"viewer":{"login":"testuser"}}`, string(data))
		assert.Equal(t, int32(2), callCount.Load())
	})

	t.Run("does NOT retry primary rate limit (403 with API RATE LIMIT EXCEEDED)", func(t *testing.T) {
		var callCount atomic.Int32
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			callCount.Add(1)
			w.WriteHeader(http.StatusForbidden)
			fmt.Fprint(w, `{"message":"API RATE LIMIT EXCEEDED"}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		_, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "403")
		assert.Equal(t, int32(1), callCount.Load()) // no retry
	})

	t.Run("fails after max retries exceeded", func(t *testing.T) {
		var callCount atomic.Int32
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			callCount.Add(1)
			w.Header().Set("Retry-After", "0")
			w.WriteHeader(http.StatusTooManyRequests)
			fmt.Fprint(w, `{"message":"rate limited"}`)
		}))
		defer server.Close()

		log := logger.New(false)
		gql := newGraphQLClient(server.URL, "test-pat", "1.0.0", log)

		_, err := gql.Post(context.Background(), "query { viewer { login } }", nil)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "secondary rate limit")
		// Initial attempt + 3 retries = 4 total
		assert.Equal(t, int32(4), callCount.Load())
	})
}
