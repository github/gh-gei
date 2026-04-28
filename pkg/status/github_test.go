package status

import (
	"context"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestGetUnresolvedIncidentsCount(t *testing.T) {
	t.Run("returns count of unresolved incidents", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, "/api/v2/incidents/unresolved.json", r.URL.Path)
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"incidents":[{"id":"1","name":"incident1"},{"id":"2","name":"incident2"}]}`))
		}))
		defer server.Close()

		count, err := GetUnresolvedIncidentsCount(context.Background(), &http.Client{}, server.URL)

		require.NoError(t, err)
		assert.Equal(t, 2, count)
	})

	t.Run("returns zero when no incidents", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"incidents":[]}`))
		}))
		defer server.Close()

		count, err := GetUnresolvedIncidentsCount(context.Background(), &http.Client{}, server.URL)

		require.NoError(t, err)
		assert.Equal(t, 0, count)
	})

	t.Run("returns error on network failure", func(t *testing.T) {
		_, err := GetUnresolvedIncidentsCount(context.Background(), &http.Client{}, "http://localhost:1")

		assert.Error(t, err)
	})

	t.Run("returns error on non-200 status", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusServiceUnavailable)
		}))
		defer server.Close()

		_, err := GetUnresolvedIncidentsCount(context.Background(), &http.Client{}, server.URL)

		assert.Error(t, err)
	})

	t.Run("returns error on invalid JSON", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`not json`))
		}))
		defer server.Close()

		_, err := GetUnresolvedIncidentsCount(context.Background(), &http.Client{}, server.URL)

		assert.Error(t, err)
	})

	t.Run("returns zero when incidents key missing", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte(`{"page":{}}`))
		}))
		defer server.Close()

		// When "incidents" key is missing, the result should be 0 incidents (empty array default)
		count, err := GetUnresolvedIncidentsCount(context.Background(), &http.Client{}, server.URL)

		require.NoError(t, err)
		assert.Equal(t, 0, count)
	})
}
