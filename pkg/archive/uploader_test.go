package archive_test

import (
	"bytes"
	"context"
	"errors"
	"io"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/github/gh-gei/pkg/archive"
)

// --- Mock implementations ---

type mockAzureUploader struct {
	uploadFn func(ctx context.Context, fileName string, content io.Reader, size int64) (string, error)
}

func (m *mockAzureUploader) Upload(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
	if m.uploadFn != nil {
		return m.uploadFn(ctx, fileName, content, size)
	}
	return "https://azure.blob.example.com/archive", nil
}

type mockAWSUploader struct {
	uploadFn func(ctx context.Context, bucket, key string, data io.Reader) (string, error)
}

func (m *mockAWSUploader) Upload(ctx context.Context, bucket, key string, data io.Reader) (string, error) {
	if m.uploadFn != nil {
		return m.uploadFn(ctx, bucket, key, data)
	}
	return "https://s3.amazonaws.com/bucket/key", nil
}

type mockGitHubUploader struct {
	uploadFn func(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error)
}

func (m *mockGitHubUploader) Upload(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
	if m.uploadFn != nil {
		return m.uploadFn(ctx, orgDatabaseID, archiveName, content, size)
	}
	return "gei://github-owned/archive", nil
}

type mockOrgIDResolver struct {
	getOrgDBIDFn func(ctx context.Context, org string) (string, error)
}

func (m *mockOrgIDResolver) GetOrganizationDatabaseId(ctx context.Context, org string) (string, error) {
	if m.getOrgDBIDFn != nil {
		return m.getOrgDBIDFn(ctx, org)
	}
	return "12345", nil
}

// --- Tests ---

func TestUpload_UsesGitHubStorage_WhenConfigured(t *testing.T) {
	// GitHub-owned storage has highest priority; even when AWS and Azure are also set.
	var capturedOrgID, capturedName string
	expectedURL := "gei://github-owned/test-archive"

	gh := &mockGitHubUploader{
		uploadFn: func(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
			capturedOrgID = orgDatabaseID
			capturedName = archiveName
			return expectedURL, nil
		},
	}
	resolver := &mockOrgIDResolver{
		getOrgDBIDFn: func(ctx context.Context, org string) (string, error) {
			assert.Equal(t, "target-org", org)
			return "99999", nil
		},
	}

	u := archive.NewUploader(
		archive.WithGitHub(gh, resolver),
		archive.WithAWS(&mockAWSUploader{}, "some-bucket"),
		archive.WithAzure(&mockAzureUploader{}),
	)

	content := bytes.NewReader([]byte("archive data"))
	url, err := u.Upload(context.Background(), "target-org", "test-archive.tar.gz", content, int64(content.Len()))

	require.NoError(t, err)
	assert.Equal(t, expectedURL, url)
	assert.Equal(t, "99999", capturedOrgID)
	assert.Equal(t, "test-archive.tar.gz", capturedName)
}

func TestUpload_UsesAWS_WhenGitHubNotConfigured(t *testing.T) {
	var capturedBucket, capturedKey string
	expectedURL := "https://s3.amazonaws.com/my-bucket/archive.tar.gz?presigned"

	awsUp := &mockAWSUploader{
		uploadFn: func(ctx context.Context, bucket, key string, data io.Reader) (string, error) {
			capturedBucket = bucket
			capturedKey = key
			return expectedURL, nil
		},
	}

	u := archive.NewUploader(
		archive.WithAWS(awsUp, "my-bucket"),
		archive.WithAzure(&mockAzureUploader{}),
	)

	content := bytes.NewReader([]byte("archive data"))
	url, err := u.Upload(context.Background(), "target-org", "archive.tar.gz", content, int64(content.Len()))

	require.NoError(t, err)
	assert.Equal(t, expectedURL, url)
	assert.Equal(t, "my-bucket", capturedBucket)
	assert.Equal(t, "archive.tar.gz", capturedKey)
}

func TestUpload_UsesAzure_AsFallback(t *testing.T) {
	var capturedFileName string
	expectedURL := "https://azure.blob.example.com/migration-archives/archive.tar.gz?sas=token"

	azureUp := &mockAzureUploader{
		uploadFn: func(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
			capturedFileName = fileName
			return expectedURL, nil
		},
	}

	u := archive.NewUploader(
		archive.WithAzure(azureUp),
	)

	content := bytes.NewReader([]byte("archive data"))
	url, err := u.Upload(context.Background(), "target-org", "archive.tar.gz", content, int64(content.Len()))

	require.NoError(t, err)
	assert.Equal(t, expectedURL, url)
	assert.Equal(t, "archive.tar.gz", capturedFileName)
}

func TestUpload_ReturnsError_WhenNoBackendConfigured(t *testing.T) {
	u := archive.NewUploader()

	content := bytes.NewReader([]byte("archive data"))
	_, err := u.Upload(context.Background(), "target-org", "archive.tar.gz", content, int64(content.Len()))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "no upload destination configured")
}

func TestUpload_Priority_GitHubOverAWSOverAzure(t *testing.T) {
	// When all three are configured, GitHub should win.
	ghCalled := false
	awsCalled := false
	azureCalled := false

	gh := &mockGitHubUploader{
		uploadFn: func(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
			ghCalled = true
			return "gei://gh", nil
		},
	}
	awsUp := &mockAWSUploader{
		uploadFn: func(ctx context.Context, bucket, key string, data io.Reader) (string, error) {
			awsCalled = true
			return "https://aws", nil
		},
	}
	azureUp := &mockAzureUploader{
		uploadFn: func(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
			azureCalled = true
			return "https://azure", nil
		},
	}

	u := archive.NewUploader(
		archive.WithGitHub(gh, &mockOrgIDResolver{}),
		archive.WithAWS(awsUp, "bucket"),
		archive.WithAzure(azureUp),
	)

	content := bytes.NewReader([]byte("data"))
	_, err := u.Upload(context.Background(), "org", "file.tar.gz", content, int64(content.Len()))

	require.NoError(t, err)
	assert.True(t, ghCalled, "GitHub uploader should have been called")
	assert.False(t, awsCalled, "AWS uploader should NOT have been called")
	assert.False(t, azureCalled, "Azure uploader should NOT have been called")
}

func TestUpload_Priority_AWSOverAzure(t *testing.T) {
	// When GitHub not configured but AWS and Azure are, AWS should win.
	awsCalled := false
	azureCalled := false

	awsUp := &mockAWSUploader{
		uploadFn: func(ctx context.Context, bucket, key string, data io.Reader) (string, error) {
			awsCalled = true
			return "https://aws", nil
		},
	}
	azureUp := &mockAzureUploader{
		uploadFn: func(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
			azureCalled = true
			return "https://azure", nil
		},
	}

	u := archive.NewUploader(
		archive.WithAWS(awsUp, "bucket"),
		archive.WithAzure(azureUp),
	)

	content := bytes.NewReader([]byte("data"))
	_, err := u.Upload(context.Background(), "org", "file.tar.gz", content, int64(content.Len()))

	require.NoError(t, err)
	assert.True(t, awsCalled, "AWS uploader should have been called")
	assert.False(t, azureCalled, "Azure uploader should NOT have been called")
}

func TestUpload_GitHubStorage_OrgIDResolverError_ReturnsError(t *testing.T) {
	resolver := &mockOrgIDResolver{
		getOrgDBIDFn: func(ctx context.Context, org string) (string, error) {
			return "", errors.New("org not found")
		},
	}

	u := archive.NewUploader(
		archive.WithGitHub(&mockGitHubUploader{}, resolver),
	)

	content := bytes.NewReader([]byte("data"))
	_, err := u.Upload(context.Background(), "bad-org", "file.tar.gz", content, int64(content.Len()))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "org not found")
}

func TestUpload_GitHubStorage_UploadError_ReturnsError(t *testing.T) {
	gh := &mockGitHubUploader{
		uploadFn: func(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
			return "", errors.New("upload failed")
		},
	}

	u := archive.NewUploader(
		archive.WithGitHub(gh, &mockOrgIDResolver{}),
	)

	content := bytes.NewReader([]byte("data"))
	_, err := u.Upload(context.Background(), "org", "file.tar.gz", content, int64(content.Len()))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "upload failed")
}

func TestUpload_AWS_UploadError_ReturnsError(t *testing.T) {
	awsUp := &mockAWSUploader{
		uploadFn: func(ctx context.Context, bucket, key string, data io.Reader) (string, error) {
			return "", errors.New("S3 access denied")
		},
	}

	u := archive.NewUploader(
		archive.WithAWS(awsUp, "bucket"),
	)

	content := bytes.NewReader([]byte("data"))
	_, err := u.Upload(context.Background(), "org", "file.tar.gz", content, int64(content.Len()))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "S3 access denied")
}

func TestUpload_Azure_UploadError_ReturnsError(t *testing.T) {
	azureUp := &mockAzureUploader{
		uploadFn: func(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
			return "", errors.New("blob storage error")
		},
	}

	u := archive.NewUploader(
		archive.WithAzure(azureUp),
	)

	content := bytes.NewReader([]byte("data"))
	_, err := u.Upload(context.Background(), "org", "file.tar.gz", content, int64(content.Len()))

	require.Error(t, err)
	assert.Contains(t, err.Error(), "blob storage error")
}
