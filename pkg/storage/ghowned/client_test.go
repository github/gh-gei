package ghowned_test

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/storage/ghowned"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestNewClient_DefaultPartSize(t *testing.T) {
	c := ghowned.NewClient("https://uploads.example.com", http.DefaultClient)
	assert.Equal(t, int64(100*1024*1024), c.PartSize())
}

func TestNewClient_WithPartSize(t *testing.T) {
	c := ghowned.NewClient("https://uploads.example.com", http.DefaultClient,
		ghowned.WithPartSize(50*1024*1024))
	assert.Equal(t, int64(50*1024*1024), c.PartSize())
}

func TestNewClient_WithPartSizeBelowMinimum(t *testing.T) {
	log := logger.New(false, io.Discard)
	c := ghowned.NewClient("https://uploads.example.com", http.DefaultClient,
		ghowned.WithLogger(log),
		ghowned.WithPartSizeMebibytes(2)) // 2 MiB < 5 MiB minimum
	// Should keep the default 100 MiB when below minimum 5 MiB
	assert.Equal(t, int64(100*1024*1024), c.PartSize())
}

func TestNewClient_WithPartSizeAtMinimum(t *testing.T) {
	c := ghowned.NewClient("https://uploads.example.com", http.DefaultClient,
		ghowned.WithPartSizeMebibytes(5))
	assert.Equal(t, int64(5*1024*1024), c.PartSize())
}

func TestUpload_SmallArchive_SinglePost(t *testing.T) {
	// Archive smaller than part size → single POST
	archiveContent := bytes.Repeat([]byte("a"), 1024) // 1 KiB
	expectedURI := "gei://archive/123"

	var receivedBody []byte
	var receivedPath string
	var receivedMethod string

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		receivedMethod = r.Method
		receivedPath = r.URL.Path + "?" + r.URL.RawQuery
		receivedBody, _ = io.ReadAll(r.Body)
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]string{"uri": expectedURI})
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client())
	uri, err := c.Upload(context.Background(), "12345", "my-archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.NoError(t, err)
	assert.Equal(t, expectedURI, uri)
	assert.Equal(t, http.MethodPost, receivedMethod)
	assert.Equal(t, "/organizations/12345/gei/archive?name=my-archive.tar.gz", receivedPath)
	assert.Equal(t, archiveContent, receivedBody)
}

func TestUpload_SmallArchive_ExactlyAtLimit(t *testing.T) {
	// Archive exactly at part size → single POST (<=, not <)
	partSize := int64(1024)
	archiveContent := bytes.Repeat([]byte("x"), int(partSize))
	expectedURI := "gei://exact"

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]string{"uri": expectedURI})
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	uri, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.NoError(t, err)
	assert.Equal(t, expectedURI, uri)
}

// Test path constants to avoid goconst warnings.
const (
	testUploadP1   = "/upload/p1"
	testUploadP2   = "/upload/p2"
	testUploadP3   = "/upload/p3"
	testUploadDone = "/upload/done"
)

func TestUpload_LargeArchive_MultipartUpload(t *testing.T) {
	// Archive larger than part size → multipart: Start → Parts → Complete
	partSize := int64(10)                           // 10 bytes for testing
	archiveContent := bytes.Repeat([]byte("b"), 25) // 25 bytes → 3 parts (10, 10, 5)
	expectedURI := "gei://multipart/done"

	var requestLog []string
	var receivedBodies [][]byte

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		path := r.URL.Path
		body, _ := io.ReadAll(r.Body)
		requestLog = append(requestLog, fmt.Sprintf("%s %s", r.Method, path))
		receivedBodies = append(receivedBodies, body)

		if r.Method == http.MethodPost && strings.HasSuffix(path, "/blobs/uploads") {
			w.Header().Set("Location", "/upload/part1")
			w.WriteHeader(http.StatusAccepted)
			return
		}
		if r.Method == http.MethodPatch {
			switch path {
			case "/upload/part1":
				w.Header().Set("Location", "/upload/part2")
				w.WriteHeader(http.StatusAccepted)
			case "/upload/part2":
				w.Header().Set("Location", "/upload/part3")
				w.WriteHeader(http.StatusAccepted)
			case "/upload/part3":
				w.Header().Set("Location", "/upload/complete")
				w.WriteHeader(http.StatusAccepted)
			default:
				w.WriteHeader(http.StatusNotFound)
			}
			return
		}
		if r.Method == http.MethodPut && path == "/upload/complete" {
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(map[string]string{"uri": expectedURI})
			return
		}
		w.WriteHeader(http.StatusNotFound)
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	uri, err := c.Upload(context.Background(), "org1", "big-archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.NoError(t, err)
	assert.Equal(t, expectedURI, uri)

	// Verify the request sequence
	require.Len(t, requestLog, 5)
	assert.Equal(t, "POST /organizations/org1/gei/archive/blobs/uploads", requestLog[0])
	assert.Equal(t, "PATCH /upload/part1", requestLog[1])
	assert.Equal(t, "PATCH /upload/part2", requestLog[2])
	assert.Equal(t, "PATCH /upload/part3", requestLog[3])
	assert.Equal(t, "PUT /upload/complete", requestLog[4])

	// Verify start body has correct JSON
	var startBody map[string]interface{}
	require.NoError(t, json.Unmarshal(receivedBodies[0], &startBody))
	assert.Equal(t, "application/octet-stream", startBody["content_type"])
	assert.Equal(t, "big-archive.tar.gz", startBody["name"])
	assert.Equal(t, float64(25), startBody["size"])

	// Verify part sizes: 10, 10, 5
	assert.Len(t, receivedBodies[1], 10)
	assert.Len(t, receivedBodies[2], 10)
	assert.Len(t, receivedBodies[3], 5)

	// Verify complete body is empty
	assert.Empty(t, receivedBodies[4])
}

func TestUpload_MultipartUpload_MissingLocationOnStart(t *testing.T) {
	partSize := int64(10)
	archiveContent := bytes.Repeat([]byte("c"), 20)

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Start response with no Location header
		w.WriteHeader(http.StatusAccepted)
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	_, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "Location")
}

func TestUpload_MultipartUpload_MissingLocationOnPart(t *testing.T) {
	partSize := int64(10)
	archiveContent := bytes.Repeat([]byte("d"), 25)

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodPost:
			w.Header().Set("Location", "/upload/part1")
			w.WriteHeader(http.StatusAccepted)
		case http.MethodPatch:
			// Missing Location header on part response
			w.WriteHeader(http.StatusAccepted)
		}
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	_, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "Location")
}

func TestUpload_SmallArchive_ServerError(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusInternalServerError)
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client())
	_, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader([]byte("data")), 4)

	require.Error(t, err)
}

func TestUpload_LargeArchive_RelativeLocationHeader(t *testing.T) {
	// Location headers can be relative; they should resolve against the uploads URL
	partSize := int64(10)
	archiveContent := bytes.Repeat([]byte("e"), 15) // 2 parts: 10, 5
	expectedURI := "gei://relative/done"

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method == http.MethodPost && strings.HasSuffix(r.URL.Path, "/blobs/uploads") {
			w.Header().Set("Location", "/relative/part1")
			w.WriteHeader(http.StatusAccepted)
			return
		}
		if r.Method == http.MethodPatch {
			switch r.URL.Path {
			case "/relative/part1":
				w.Header().Set("Location", "/relative/part2")
				w.WriteHeader(http.StatusAccepted)
			case "/relative/part2":
				w.Header().Set("Location", "/relative/complete")
				w.WriteHeader(http.StatusAccepted)
			default:
				w.WriteHeader(http.StatusNotFound)
			}
			return
		}
		if r.Method == http.MethodPut && r.URL.Path == "/relative/complete" {
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(map[string]string{"uri": expectedURI})
			return
		}
		w.WriteHeader(http.StatusNotFound)
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	uri, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.NoError(t, err)
	assert.Equal(t, expectedURI, uri)
}

func TestUpload_SmallArchive_InvalidJSON(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte("not json"))
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client())
	_, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader([]byte("data")), 4)

	require.Error(t, err)
}

func TestUpload_WithLogger(t *testing.T) {
	partSize := int64(10)
	archiveContent := bytes.Repeat([]byte("f"), 25) // 3 parts
	expectedURI := "gei://logged"

	var logBuf bytes.Buffer
	log := logger.New(false, &logBuf)

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method == http.MethodPost && strings.HasSuffix(r.URL.Path, "/blobs/uploads") {
			w.Header().Set("Location", testUploadP1)
			w.WriteHeader(http.StatusAccepted)
			return
		}
		if r.Method == http.MethodPatch {
			switch r.URL.Path {
			case testUploadP1:
				w.Header().Set("Location", testUploadP2)
				w.WriteHeader(http.StatusAccepted)
			case testUploadP2:
				w.Header().Set("Location", testUploadP3)
				w.WriteHeader(http.StatusAccepted)
			case testUploadP3:
				w.Header().Set("Location", testUploadDone)
				w.WriteHeader(http.StatusAccepted)
			}
			return
		}
		if r.Method == http.MethodPut && r.URL.Path == testUploadDone {
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(map[string]string{"uri": expectedURI})
			return
		}
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(),
		ghowned.WithPartSize(partSize),
		ghowned.WithLogger(log))
	uri, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.NoError(t, err)
	assert.Equal(t, expectedURI, uri)

	logOutput := logBuf.String()
	assert.Contains(t, logOutput, "Uploading part 1/3")
	assert.Contains(t, logOutput, "Uploading part 2/3")
	assert.Contains(t, logOutput, "Uploading part 3/3")
	assert.Contains(t, logOutput, "Finished uploading archive")
}

func TestUpload_MultipartComplete_ServerError(t *testing.T) {
	partSize := int64(10)
	archiveContent := bytes.Repeat([]byte("g"), 15) // 2 parts

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodPost:
			w.Header().Set("Location", testUploadP1)
			w.WriteHeader(http.StatusAccepted)
		case http.MethodPatch:
			switch r.URL.Path {
			case testUploadP1:
				w.Header().Set("Location", testUploadP2)
				w.WriteHeader(http.StatusAccepted)
			case testUploadP2:
				w.Header().Set("Location", testUploadDone)
				w.WriteHeader(http.StatusAccepted)
			}
		case http.MethodPut:
			w.WriteHeader(http.StatusInternalServerError)
		}
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	_, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.Error(t, err)
}

func TestUpload_PatchContentType(t *testing.T) {
	// Verify PATCH requests use application/octet-stream content type
	partSize := int64(10)
	archiveContent := bytes.Repeat([]byte("h"), 15)
	var patchContentType string

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodPost:
			w.Header().Set("Location", testUploadP1)
			w.WriteHeader(http.StatusAccepted)
		case http.MethodPatch:
			if r.URL.Path == testUploadP1 {
				patchContentType = r.Header.Get("Content-Type")
			}
			switch r.URL.Path {
			case testUploadP1:
				w.Header().Set("Location", testUploadP2)
				w.WriteHeader(http.StatusAccepted)
			case testUploadP2:
				w.Header().Set("Location", testUploadDone)
				w.WriteHeader(http.StatusAccepted)
			}
		case http.MethodPut:
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(map[string]string{"uri": "gei://test"})
		}
	}))
	defer srv.Close()

	c := ghowned.NewClient(srv.URL, srv.Client(), ghowned.WithPartSize(partSize))
	_, err := c.Upload(context.Background(), "org1", "archive.tar.gz",
		bytes.NewReader(archiveContent), int64(len(archiveContent)))

	require.NoError(t, err)
	assert.Equal(t, "application/octet-stream", patchContentType)
}
