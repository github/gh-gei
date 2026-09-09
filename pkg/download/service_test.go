package download

import (
	"context"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestDownloadToFile_Success(t *testing.T) {
	expectedContent := "this is the log file content"
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(expectedContent))
	}))
	defer srv.Close()

	svc := New(srv.Client())

	dest := filepath.Join(t.TempDir(), "output.log")
	err := svc.DownloadToFile(context.Background(), srv.URL+"/log", dest)
	require.NoError(t, err)

	got, err := os.ReadFile(dest)
	require.NoError(t, err)
	assert.Equal(t, expectedContent, string(got))
}

func TestDownloadToFile_Non200Error(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusNotFound)
	}))
	defer srv.Close()

	svc := New(srv.Client())

	dest := filepath.Join(t.TempDir(), "output.log")
	err := svc.DownloadToFile(context.Background(), srv.URL+"/missing", dest)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "404")
}

func TestDownloadToBytes_Success(t *testing.T) {
	expectedContent := "byte content here"
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte(expectedContent))
	}))
	defer srv.Close()

	svc := New(srv.Client())

	got, err := svc.DownloadToBytes(context.Background(), srv.URL+"/data")
	require.NoError(t, err)
	assert.Equal(t, []byte(expectedContent), got)
}

func TestDownloadToBytes_Non200Error(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusInternalServerError)
	}))
	defer srv.Close()

	svc := New(srv.Client())

	_, err := svc.DownloadToBytes(context.Background(), srv.URL+"/fail")
	require.Error(t, err)
	assert.Contains(t, err.Error(), "500")
}

func TestNew_NilClient_UsesDefault(t *testing.T) {
	svc := New(nil)
	require.NotNil(t, svc)
	require.NotNil(t, svc.client)
	assert.Equal(t, defaultTimeout, svc.client.Timeout)
}
