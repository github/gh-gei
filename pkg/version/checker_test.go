package version

import (
	"context"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestParseVersion(t *testing.T) {
	tests := []struct {
		name    string
		input   string
		want    semver
		wantErr bool
	}{
		{"three parts", "1.27.0", semver{1, 27, 0}, false},
		{"with v prefix", "v1.27.0", semver{1, 27, 0}, false},
		{"with V prefix", "V1.27.0", semver{1, 27, 0}, false},
		{"with whitespace", "  v1.27.0\n", semver{1, 27, 0}, false},
		{"two parts", "1.27", semver{0, 0, 0}, true},
		{"empty", "", semver{0, 0, 0}, true},
		{"non-numeric", "a.b.c", semver{0, 0, 0}, true},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got, err := parseVersion(tt.input)
			if tt.wantErr {
				assert.Error(t, err)
				return
			}
			require.NoError(t, err)
			assert.Equal(t, tt.want, got)
		})
	}
}

func TestSemverCompare(t *testing.T) {
	tests := []struct {
		name string
		a, b semver
		want int
	}{
		{"equal", semver{1, 2, 3}, semver{1, 2, 3}, 0},
		{"major greater", semver{2, 0, 0}, semver{1, 9, 9}, 1},
		{"major less", semver{1, 0, 0}, semver{2, 0, 0}, -1},
		{"minor greater", semver{1, 3, 0}, semver{1, 2, 9}, 1},
		{"minor less", semver{1, 2, 0}, semver{1, 3, 0}, -1},
		{"patch greater", semver{1, 2, 4}, semver{1, 2, 3}, 1},
		{"patch less", semver{1, 2, 3}, semver{1, 2, 4}, -1},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tt.a.compare(tt.b)
			assert.Equal(t, tt.want, got)
		})
	}
}

func TestChecker_IsLatest(t *testing.T) {
	t.Run("current version equals latest returns true", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("v1.27.0\n"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		isLatest, err := checker.IsLatest(context.Background())

		require.NoError(t, err)
		assert.True(t, isLatest)
	})

	t.Run("current version greater than latest returns true", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("v1.26.0\n"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		isLatest, err := checker.IsLatest(context.Background())

		require.NoError(t, err)
		assert.True(t, isLatest)
	})

	t.Run("current version less than latest returns false", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("v1.28.0\n"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		isLatest, err := checker.IsLatest(context.Background())

		require.NoError(t, err)
		assert.False(t, isLatest)
	})

	t.Run("network error returns error", func(t *testing.T) {
		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = "http://localhost:1" // nothing listening

		_, err := checker.IsLatest(context.Background())

		assert.Error(t, err)
	})

	t.Run("server returns non-200 returns error", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusInternalServerError)
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		_, err := checker.IsLatest(context.Background())

		assert.Error(t, err)
	})

	t.Run("server returns invalid version returns error", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("not-a-version"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		_, err := checker.IsLatest(context.Background())

		assert.Error(t, err)
	})

	t.Run("caches latest version after first fetch", func(t *testing.T) {
		calls := 0
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			calls++
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("v1.27.0\n"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		_, _ = checker.IsLatest(context.Background())
		_, _ = checker.IsLatest(context.Background())

		assert.Equal(t, 1, calls)
	})

	t.Run("sets user-agent header", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			assert.Equal(t, "OctoshiftCLI/1.27.0", r.Header.Get("User-Agent"))
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("v1.27.0\n"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		_, _ = checker.IsLatest(context.Background())
	})
}

func TestChecker_GetLatestVersion(t *testing.T) {
	t.Run("returns fetched version string", func(t *testing.T) {
		server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			w.WriteHeader(http.StatusOK)
			w.Write([]byte("v1.28.0\n"))
		}))
		defer server.Close()

		log := logger.New(false)
		checker := NewChecker(&http.Client{}, log, "1.27.0")
		checker.versionURL = server.URL

		ver, err := checker.GetLatestVersion(context.Background())

		require.NoError(t, err)
		assert.Equal(t, "1.28.0", ver)
	})
}
