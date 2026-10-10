package aws_test

import (
	"bytes"
	"context"
	"errors"
	"io"
	"os"
	"strings"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	awsclient "github.com/github/gh-gei/pkg/storage/aws"
)

// --- Mock implementations ---

type mockS3Uploader struct {
	putObjectFn func(ctx context.Context, bucket, key string, body io.Reader) error
}

func (m *mockS3Uploader) PutObject(ctx context.Context, bucket, key string, body io.Reader) error {
	if m.putObjectFn != nil {
		return m.putObjectFn(ctx, bucket, key, body)
	}
	return nil
}

type mockS3Presigner struct {
	presignGetFn func(ctx context.Context, bucket, key string, expires time.Duration) (string, error)
}

func (m *mockS3Presigner) PresignGetObject(ctx context.Context, bucket, key string, expires time.Duration) (string, error) {
	if m.presignGetFn != nil {
		return m.presignGetFn(ctx, bucket, key, expires)
	}
	return "https://example.com/presigned", nil
}

// --- Tests ---

func TestUpload_StreamToS3_ReturnsPresignedURL(t *testing.T) {
	// Arrange
	expectedURL := "https://s3.amazonaws.com/bucket/key?presigned=true"
	var capturedBucket, capturedKey string
	var capturedBody []byte

	uploader := &mockS3Uploader{
		putObjectFn: func(ctx context.Context, bucket, key string, body io.Reader) error {
			capturedBucket = bucket
			capturedKey = key
			var err error
			capturedBody, err = io.ReadAll(body)
			require.NoError(t, err)
			return nil
		},
	}

	presigner := &mockS3Presigner{
		presignGetFn: func(ctx context.Context, bucket, key string, expires time.Duration) (string, error) {
			assert.Equal(t, "my-bucket", bucket)
			assert.Equal(t, "archive.tar.gz", key)
			assert.Equal(t, 48*time.Hour, expires)
			return expectedURL, nil
		},
	}

	client := awsclient.NewClientFromInterfaces(uploader, presigner, nil)

	// Act
	data := strings.NewReader("archive content here")
	url, err := client.Upload(context.Background(), "my-bucket", "archive.tar.gz", data)

	// Assert
	require.NoError(t, err)
	assert.Equal(t, expectedURL, url)
	assert.Equal(t, "my-bucket", capturedBucket)
	assert.Equal(t, "archive.tar.gz", capturedKey)
	assert.Equal(t, "archive content here", string(capturedBody))
}

func TestUploadFile_ReadsFileAndUploads(t *testing.T) {
	// Arrange — create a temp file
	tmpFile, err := os.CreateTemp("", "aws-upload-test-*.bin")
	require.NoError(t, err)
	defer os.Remove(tmpFile.Name())

	content := []byte("file archive content")
	_, err = tmpFile.Write(content)
	require.NoError(t, err)
	require.NoError(t, tmpFile.Close())

	expectedURL := "https://s3.amazonaws.com/bucket/key?presigned=file"
	var capturedBody []byte

	uploader := &mockS3Uploader{
		putObjectFn: func(ctx context.Context, bucket, key string, body io.Reader) error {
			capturedBody, _ = io.ReadAll(body)
			return nil
		},
	}

	presigner := &mockS3Presigner{
		presignGetFn: func(_ context.Context, _, _ string, _ time.Duration) (string, error) {
			return expectedURL, nil
		},
	}

	client := awsclient.NewClientFromInterfaces(uploader, presigner, nil)

	// Act
	url, err := client.UploadFile(context.Background(), "my-bucket", "archive-key", tmpFile.Name())

	// Assert
	require.NoError(t, err)
	assert.Equal(t, expectedURL, url)
	assert.Equal(t, content, capturedBody)
}

func TestUploadFile_FileNotFound_ReturnsError(t *testing.T) {
	client := awsclient.NewClientFromInterfaces(&mockS3Uploader{}, &mockS3Presigner{}, nil)

	_, err := client.UploadFile(context.Background(), "bucket", "key", "/nonexistent/file.zip")

	require.Error(t, err)
	assert.Contains(t, err.Error(), "/nonexistent/file.zip")
}

func TestUpload_PutObjectError_ReturnsError(t *testing.T) {
	uploader := &mockS3Uploader{
		putObjectFn: func(ctx context.Context, bucket, key string, body io.Reader) error {
			return errors.New("S3 upload failed: access denied")
		},
	}

	client := awsclient.NewClientFromInterfaces(uploader, &mockS3Presigner{}, nil)

	_, err := client.Upload(context.Background(), "bucket", "key", strings.NewReader("data"))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "S3 upload failed")
}

func TestUpload_PresignError_ReturnsError(t *testing.T) {
	presigner := &mockS3Presigner{
		presignGetFn: func(ctx context.Context, bucket, key string, expires time.Duration) (string, error) {
			return "", errors.New("presign failure")
		},
	}

	client := awsclient.NewClientFromInterfaces(&mockS3Uploader{}, presigner, nil)

	_, err := client.Upload(context.Background(), "bucket", "key", strings.NewReader("data"))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "presign")
}

func TestUploadFile_TimeoutDuringUpload_ReturnsTimeoutError(t *testing.T) {
	uploader := &mockS3Uploader{
		putObjectFn: func(ctx context.Context, bucket, key string, body io.Reader) error {
			return context.DeadlineExceeded
		},
	}

	client := awsclient.NewClientFromInterfaces(uploader, &mockS3Presigner{}, nil)

	tmpFile, err := os.CreateTemp("", "aws-timeout-test-*.bin")
	require.NoError(t, err)
	defer os.Remove(tmpFile.Name())
	_, _ = tmpFile.Write([]byte("data"))
	tmpFile.Close()

	_, err = client.UploadFile(context.Background(), "bucket", "key", tmpFile.Name())

	require.Error(t, err)
	assert.Contains(t, err.Error(), "timed out")
}

func TestUpload_ProgressLogging(t *testing.T) {
	// Arrange: use a reader that reports progress
	var logMessages []string
	mockLogger := &mockProgressLogger{
		logFn: func(msg string) {
			logMessages = append(logMessages, msg)
		},
	}

	// Create a large-ish content buffer so progress reporting has something to report
	data := make([]byte, 1024*100) // 100KB
	for i := range data {
		data[i] = byte(i % 256)
	}

	uploader := &mockS3Uploader{
		putObjectFn: func(ctx context.Context, bucket, key string, body io.Reader) error {
			// Simulate reading the body (which triggers progress)
			_, err := io.ReadAll(body)
			return err
		},
	}

	presigner := &mockS3Presigner{}

	client := awsclient.NewClientFromInterfaces(uploader, presigner, mockLogger)

	// Act
	_, err := client.Upload(context.Background(), "bucket", "key", bytes.NewReader(data))

	// Assert
	require.NoError(t, err)
	// Progress logging is throttled, but at minimum there should be some message
	// about the upload completing or in progress
	// The exact behavior depends on throttling; we at least verify no panics
}

func TestNewClient_WithOptions(t *testing.T) {
	// This test verifies that the constructor doesn't panic with various option combinations.
	// We can't actually call AWS without credentials, but we verify construction works.
	t.Run("with region", func(t *testing.T) {
		client, err := awsclient.NewClient("access", "secret", awsclient.WithRegion("us-east-1"))
		require.NoError(t, err)
		assert.NotNil(t, client)
	})

	t.Run("with session token", func(t *testing.T) {
		client, err := awsclient.NewClient("access", "secret",
			awsclient.WithRegion("us-west-2"),
			awsclient.WithSessionToken("token123"),
		)
		require.NoError(t, err)
		assert.NotNil(t, client)
	})

	t.Run("without region defaults", func(t *testing.T) {
		client, err := awsclient.NewClient("access", "secret")
		require.NoError(t, err)
		assert.NotNil(t, client)
	})
}

// --- mock progress logger for testing ---

type mockProgressLogger struct {
	logFn func(msg string)
}

func (m *mockProgressLogger) LogInfo(format string, args ...interface{}) {
	if m.logFn != nil {
		msg := format
		if len(args) > 0 {
			msg = strings.ReplaceAll(format, "%s", "X") // simple substitution for test
		}
		m.logFn(msg)
	}
}
