// Package azure provides a client for interacting with Azure Blob Storage,
// used for uploading and downloading migration archives.
package azure

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"sync"
	"time"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/google/uuid"
)

const (
	containerPrefix        = "migration-archives"
	sasExpiryDuration      = 48 * time.Hour
	progressReportInterval = 10 * time.Second
	downloadHTTPTimeout    = 1 * time.Hour
)

// BlobService abstracts Azure Blob Storage operations for testability.
type BlobService interface {
	CreateContainer(ctx context.Context, name string) error
	UploadBlob(ctx context.Context, container, blob string, content io.Reader, size int64, progressFn func(int64)) error
	GenerateSASURL(container, blob string, expiry time.Duration) (string, error)
}

// Client provides operations for uploading to and downloading from Azure Blob Storage.
type Client struct {
	blobService BlobService
	logger      *logger.Logger
	httpClient  *http.Client

	mu                 sync.Mutex
	nextProgressReport time.Time
}

// NewClient creates a Client backed by the real Azure SDK, using the given connection string.
func NewClient(connectionString string, log *logger.Logger) (*Client, error) {
	if connectionString == "" {
		return nil, fmt.Errorf("connection string must not be empty")
	}

	svc, err := newAzureBlobService(connectionString)
	if err != nil {
		return nil, fmt.Errorf("initializing azure blob service: %w", err)
	}

	return NewClientWithService(svc, log), nil
}

// NewClientWithService creates a Client using the given BlobService implementation.
// This is primarily useful for testing with a fake BlobService.
func NewClientWithService(svc BlobService, log *logger.Logger) *Client {
	return &Client{
		blobService: svc,
		logger:      log,
		httpClient: &http.Client{
			Timeout: downloadHTTPTimeout,
		},
		nextProgressReport: time.Now(),
	}
}

// Upload uploads content to Azure Blob Storage and returns a SAS URL for the blob.
// It creates a container named "migration-archives-<uuid>", uploads the blob,
// and generates a read-only SAS URL with 48-hour expiry.
func (c *Client) Upload(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
	if fileName == "" {
		return "", fmt.Errorf("fileName must not be empty")
	}
	if content == nil {
		return "", fmt.Errorf("content must not be nil")
	}

	containerName := fmt.Sprintf("%s-%s", containerPrefix, uuid.New().String())

	if err := c.blobService.CreateContainer(ctx, containerName); err != nil {
		return "", fmt.Errorf("creating container: %w", err)
	}

	progressFn := func(uploaded int64) {
		c.logProgress(uploaded, size)
	}

	if err := c.blobService.UploadBlob(ctx, containerName, fileName, content, size, progressFn); err != nil {
		return "", fmt.Errorf("uploading blob: %w", err)
	}

	sasURL, err := c.blobService.GenerateSASURL(containerName, fileName, sasExpiryDuration)
	if err != nil {
		return "", fmt.Errorf("generating SAS URL: %w", err)
	}

	return sasURL, nil
}

// Download retrieves archive bytes from the given URL via HTTP GET.
func (c *Client) Download(ctx context.Context, url string) ([]byte, error) {
	c.logger.Verbose("HTTP GET: %s", url)

	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return nil, fmt.Errorf("creating download request: %w", err)
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("downloading archive: %w", err)
	}
	defer resp.Body.Close()

	c.logger.Verbose("RESPONSE (%d): <truncated>", resp.StatusCode)

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return nil, fmt.Errorf("download failed with HTTP %d", resp.StatusCode)
	}

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, fmt.Errorf("reading download response: %w", err)
	}

	return data, nil
}

func (c *Client) logProgress(uploadedBytes, totalBytes int64) {
	c.mu.Lock()
	if time.Now().Before(c.nextProgressReport) {
		c.mu.Unlock()
		return
	}
	c.nextProgressReport = c.nextProgressReport.Add(progressReportInterval)
	c.mu.Unlock()

	if totalBytes == 0 {
		c.logger.Info("Archive upload in progress...")
		return
	}

	percentage := int(uploadedBytes * 100 / totalBytes)
	c.logger.Info("Archive upload in progress, %s out of %s (%d%%) completed...",
		formatSize(uploadedBytes), formatSize(totalBytes), percentage)
}

// formatSize formats bytes into human-readable form.
func formatSize(bytes int64) string {
	const (
		kb = 1024
		mb = 1024 * kb
		gb = 1024 * mb
	)
	switch {
	case bytes >= gb:
		return fmt.Sprintf("%.2f GB", float64(bytes)/float64(gb))
	case bytes >= mb:
		return fmt.Sprintf("%.2f MB", float64(bytes)/float64(mb))
	case bytes >= kb:
		return fmt.Sprintf("%.2f KB", float64(bytes)/float64(kb))
	default:
		return fmt.Sprintf("%d Bytes", bytes)
	}
}
