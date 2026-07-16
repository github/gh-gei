package azure_test

import (
	"bytes"
	"context"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/storage/azure"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

const testSASURL = "https://example.com/blob?sas=token"

// fakeBlobService implements azure.BlobService for testing
type fakeBlobService struct {
	createContainerName string
	uploadedBlob        string
	uploadedData        []byte
	sasURL              string
	createContainerErr  error
	uploadErr           error
	sasErr              error
}

func (f *fakeBlobService) CreateContainer(ctx context.Context, name string) error {
	f.createContainerName = name
	return f.createContainerErr
}

func (f *fakeBlobService) UploadBlob(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error {
	f.uploadedBlob = blob
	data, err := io.ReadAll(content)
	if err != nil {
		return err
	}
	f.uploadedData = data
	if progressFn != nil {
		progressFn(int64(len(data)))
	}
	return f.uploadErr
}

func (f *fakeBlobService) GenerateSASURL(container, blob string, expiry time.Duration) (string, error) {
	if f.sasErr != nil {
		return "", f.sasErr
	}
	if f.sasURL != "" {
		return f.sasURL, nil
	}
	return "https://storage.blob.core.windows.net/" + container + "/" + blob + "?sas=test", nil
}

func TestUpload_ReturnsURLSAS(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{
		sasURL: "https://myaccount.blob.core.windows.net/migration-archives-abc/test.zip?se=2026-04-02&sig=xxx",
	}

	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("archive-content"))
	url, err := client.Upload(context.Background(), "test.zip", content, int64(content.Len()))

	require.NoError(t, err)
	assert.Equal(t, fakeSvc.sasURL, url)
	assert.Equal(t, "test.zip", fakeSvc.uploadedBlob)
	assert.Equal(t, []byte("archive-content"), fakeSvc.uploadedData)
}

func TestUpload_ContainerNameFormat(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{}

	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, int64(content.Len()))

	require.NoError(t, err)
	assert.True(t, strings.HasPrefix(fakeSvc.createContainerName, "migration-archives-"),
		"container name should start with 'migration-archives-', got: %s", fakeSvc.createContainerName)
	// UUID portion after prefix
	uuidPart := strings.TrimPrefix(fakeSvc.createContainerName, "migration-archives-")
	assert.Len(t, uuidPart, 36, "UUID portion should be 36 chars (with hyphens)")
}

func TestUpload_SASExpiry48Hours(t *testing.T) {
	log := logger.New(false)
	var capturedExpiry time.Duration
	fakeSvc := &trackingBlobService{
		onGenerateSAS: func(container, blob string, expiry time.Duration) (string, error) {
			capturedExpiry = expiry
			return testSASURL, nil
		},
	}

	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, int64(content.Len()))

	require.NoError(t, err)
	assert.Equal(t, 48*time.Hour, capturedExpiry)
}

func TestUpload_FailsOnNilFileName(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{}
	client := azure.NewClientWithService(fakeSvc, log)

	_, err := client.Upload(context.Background(), "", bytes.NewReader(nil), 0)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "fileName")
}

func TestUpload_FailsOnNilContent(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{}
	client := azure.NewClientWithService(fakeSvc, log)

	_, err := client.Upload(context.Background(), "file.zip", nil, 0)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "content")
}

func TestUpload_PropagatesContainerCreateError(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{
		createContainerErr: assert.AnError,
	}
	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, int64(content.Len()))

	require.Error(t, err)
	assert.ErrorIs(t, err, assert.AnError)
}

func TestUpload_PropagatesUploadError(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{
		uploadErr: assert.AnError,
	}
	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, int64(content.Len()))

	require.Error(t, err)
	assert.ErrorIs(t, err, assert.AnError)
}

func TestUpload_PropagatesSASError(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{
		sasErr: assert.AnError,
	}
	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, int64(content.Len()))

	require.Error(t, err)
	assert.ErrorIs(t, err, assert.AnError)
}

func TestUpload_LogsProgress(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	callCount := 0
	fakeSvc := &trackingBlobService{
		onUploadBlob: func(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error {
			// Simulate progress callbacks — first should log, subsequent depend on time
			if progressFn != nil {
				progressFn(50)
				progressFn(100)
			}
			callCount++
			return nil
		},
		onGenerateSAS: func(container, blob string, expiry time.Duration) (string, error) {
			return testSASURL, nil
		},
	}

	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, 100)

	require.NoError(t, err)
	assert.Equal(t, 1, callCount)
	// First progress report should be logged immediately
	assert.Contains(t, buf.String(), "Archive upload in progress")
}

func TestDownload_ReturnsBytes(t *testing.T) {
	expectedBody := []byte("archive-binary-data")
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write(expectedBody)
	}))
	defer server.Close()

	log := logger.New(false)
	fakeSvc := &fakeBlobService{}
	client := azure.NewClientWithService(fakeSvc, log)

	data, err := client.Download(context.Background(), server.URL+"/archive.tar.gz")

	require.NoError(t, err)
	assert.Equal(t, expectedBody, data)
}

func TestDownload_ReturnsErrorOnHTTPFailure(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusNotFound)
	}))
	defer server.Close()

	log := logger.New(false)
	fakeSvc := &fakeBlobService{}
	client := azure.NewClientWithService(fakeSvc, log)

	_, err := client.Download(context.Background(), server.URL+"/missing.tar.gz")

	require.Error(t, err)
	assert.Contains(t, err.Error(), "404")
}

func TestDownload_ReturnsErrorOnInvalidURL(t *testing.T) {
	log := logger.New(false)
	fakeSvc := &fakeBlobService{}
	client := azure.NewClientWithService(fakeSvc, log)

	_, err := client.Download(context.Background(), "://invalid")

	require.Error(t, err)
}

func TestNewClient_ValidConnectionString(t *testing.T) {
	log := logger.New(false)
	// Use Azurite-style connection string (works for validation, no real connection)
	connStr := "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;EndpointSuffix=core.windows.net"
	client, err := azure.NewClient(connStr, log)

	require.NoError(t, err)
	assert.NotNil(t, client)
}

func TestNewClient_EmptyConnectionString(t *testing.T) {
	log := logger.New(false)
	_, err := azure.NewClient("", log)

	require.Error(t, err)
	assert.Contains(t, err.Error(), "connection string")
}

func TestUpload_ProgressThrottling(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	progressCalls := 0
	fakeSvc := &trackingBlobService{
		onUploadBlob: func(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error {
			// Simulate rapid progress callbacks — only the first should be logged
			// because subsequent ones are within the 10-second throttle window
			if progressFn != nil {
				for i := 0; i < 100; i++ {
					progressFn(int64(i + 1))
					progressCalls++
				}
			}
			return nil
		},
		onGenerateSAS: func(container, blob string, expiry time.Duration) (string, error) {
			return testSASURL, nil
		},
	}

	client := azure.NewClientWithService(fakeSvc, log)

	content := bytes.NewReader([]byte("data"))
	_, err := client.Upload(context.Background(), "file.zip", content, 100)

	require.NoError(t, err)
	assert.Equal(t, 100, progressCalls, "all progress callbacks should fire")
	// Only 1 log line should appear (throttled to 10-second intervals)
	logLines := strings.Count(buf.String(), "Archive upload in progress")
	assert.Equal(t, 1, logLines, "should only log progress once within 10-second window")
}

// trackingBlobService provides custom behavior via function fields
type trackingBlobService struct {
	onCreateContainer func(ctx context.Context, name string) error
	onUploadBlob      func(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error
	onGenerateSAS     func(container, blob string, expiry time.Duration) (string, error)
}

func (t *trackingBlobService) CreateContainer(ctx context.Context, name string) error {
	if t.onCreateContainer != nil {
		return t.onCreateContainer(ctx, name)
	}
	return nil
}

func (t *trackingBlobService) UploadBlob(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error {
	if t.onUploadBlob != nil {
		return t.onUploadBlob(ctx, container, blob, content, size, progressFn)
	}
	data, _ := io.ReadAll(content)
	if progressFn != nil {
		progressFn(int64(len(data)))
	}
	return nil
}

func (t *trackingBlobService) GenerateSASURL(container, blob string, expiry time.Duration) (string, error) {
	if t.onGenerateSAS != nil {
		return t.onGenerateSAS(container, blob, expiry)
	}
	return testSASURL, nil
}
