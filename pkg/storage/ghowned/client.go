// Package ghowned implements multipart upload for GitHub-owned storage.
// It is the Go equivalent of C# ArchiveUploader.
package ghowned

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

const (
	bytesPerMebibyte       = 1024 * 1024
	minMultipartMebibytes  = 5
	defaultMultipartMebibs = 100
)

// Client uploads archives to GitHub-owned storage.
type Client struct {
	httpClient *http.Client
	uploadsURL string
	logger     *logger.Logger
	partSize   int64 // in bytes; default 100 MiB
}

// Option configures a Client.
type Option func(*Client)

// WithPartSize sets the multipart part size in bytes.
// This does no minimum enforcement — use WithPartSizeMebibytes for user-facing input.
func WithPartSize(size int64) Option {
	return func(c *Client) {
		c.partSize = size
	}
}

// WithPartSizeMebibytes sets the multipart part size from a value in mebibytes.
// If below the 5 MiB minimum, it logs a warning and keeps the default.
func WithPartSizeMebibytes(mebibs int64) Option {
	return func(c *Client) {
		if mebibs < minMultipartMebibytes {
			if c.logger != nil {
				c.logger.Warning("Multipart part size %d MiB is below minimum %d MiB, using default %d MiB",
					mebibs, minMultipartMebibytes, defaultMultipartMebibs)
			}
			return
		}
		c.partSize = mebibs * bytesPerMebibyte
	}
}

// WithLogger sets the logger.
func WithLogger(l *logger.Logger) Option {
	return func(c *Client) {
		c.logger = l
	}
}

// NewClient creates a new GitHub-owned storage upload client.
func NewClient(uploadsURL string, httpClient *http.Client, opts ...Option) *Client {
	c := &Client{
		httpClient: httpClient,
		uploadsURL: uploadsURL,
		partSize:   defaultMultipartMebibs * bytesPerMebibyte,
	}
	// Apply logger first so WithPartSize can log warnings.
	for _, opt := range opts {
		opt(c)
	}
	return c
}

// PartSize returns the configured part size in bytes.
func (c *Client) PartSize() int64 {
	return c.partSize
}

// Upload uploads an archive to GitHub-owned storage.
// For archives <= partSize it does a single POST.
// For larger archives it uses the multipart protocol: Start → Parts → Complete.
func (c *Client) Upload(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
	if size <= c.partSize {
		return c.uploadSingle(ctx, orgDatabaseID, archiveName, content, size)
	}
	return c.uploadMultipart(ctx, orgDatabaseID, archiveName, content, size)
}

// uploadSingle performs a single POST for small archives.
func (c *Client) uploadSingle(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
	uploadURL := fmt.Sprintf("%s/organizations/%s/gei/archive?name=%s",
		c.uploadsURL, orgDatabaseID, url.QueryEscape(archiveName))

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, uploadURL, content)
	if err != nil {
		return "", cmdutil.WrapUserError("failed to create upload request", err)
	}
	req.ContentLength = size

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", cmdutil.WrapUserError("failed to upload archive", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return "", cmdutil.NewUserErrorf("upload failed with status %d", resp.StatusCode)
	}

	return c.parseURIResponse(resp.Body)
}

// uploadMultipart implements the 3-phase multipart protocol.
func (c *Client) uploadMultipart(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error) {
	// Phase 1: Start
	nextURL, err := c.multipartStart(ctx, orgDatabaseID, archiveName, size)
	if err != nil {
		return "", err
	}

	// Phase 2: Parts
	totalParts := (size + c.partSize - 1) / c.partSize
	buf := make([]byte, c.partSize)

	for partNum := int64(1); partNum <= totalParts; partNum++ {
		n, readErr := io.ReadFull(content, buf)
		if readErr != nil && readErr != io.ErrUnexpectedEOF {
			return "", cmdutil.WrapUserError("failed to read archive content", readErr)
		}

		c.logInfo("Uploading part %d/%d...", partNum, totalParts)

		nextURL, err = c.multipartPart(ctx, nextURL, buf[:n])
		if err != nil {
			return "", err
		}
	}

	// Phase 3: Complete
	uri, err := c.multipartComplete(ctx, nextURL)
	if err != nil {
		return "", err
	}

	c.logInfo("Finished uploading archive")
	return uri, nil
}

// multipartStart sends the initial POST to begin a multipart upload.
func (c *Client) multipartStart(ctx context.Context, orgDatabaseID, archiveName string, size int64) (string, error) {
	startURL := fmt.Sprintf("%s/organizations/%s/gei/archive/blobs/uploads",
		c.uploadsURL, orgDatabaseID)

	body, err := json.Marshal(map[string]interface{}{
		"content_type": "application/octet-stream",
		"name":         archiveName,
		"size":         size,
	})
	if err != nil {
		return "", cmdutil.WrapUserError("failed to marshal start request", err)
	}

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, startURL, bytes.NewReader(body))
	if err != nil {
		return "", cmdutil.WrapUserError("failed to create multipart start request", err)
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", cmdutil.WrapUserError("failed to start multipart upload", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return "", cmdutil.NewUserErrorf("multipart start failed with status %d", resp.StatusCode)
	}

	return c.getNextURL(resp)
}

// multipartPart sends a PATCH with one chunk of data.
func (c *Client) multipartPart(ctx context.Context, patchURL string, data []byte) (string, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodPatch, patchURL, bytes.NewReader(data))
	if err != nil {
		return "", cmdutil.WrapUserError("failed to create part request", err)
	}
	req.Header.Set("Content-Type", "application/octet-stream")
	req.ContentLength = int64(len(data))

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", cmdutil.WrapUserError("failed to upload part", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return "", cmdutil.NewUserErrorf("part upload failed with status %d", resp.StatusCode)
	}

	return c.getNextURL(resp)
}

// multipartComplete sends the final PUT to finalize the upload.
func (c *Client) multipartComplete(ctx context.Context, putURL string) (string, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodPut, putURL, strings.NewReader(""))
	if err != nil {
		return "", cmdutil.WrapUserError("failed to create complete request", err)
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", cmdutil.WrapUserError("failed to complete multipart upload", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return "", cmdutil.NewUserErrorf("multipart complete failed with status %d", resp.StatusCode)
	}

	return c.parseURIResponse(resp.Body)
}

// getNextURL extracts and resolves the Location header from a response.
func (c *Client) getNextURL(resp *http.Response) (string, error) {
	loc := resp.Header.Get("Location")
	if loc == "" {
		return "", cmdutil.NewUserError("multipart upload response is missing Location header")
	}

	// Resolve relative URLs against the uploads base URL.
	base, err := url.Parse(c.uploadsURL)
	if err != nil {
		return "", cmdutil.WrapUserError("invalid uploads URL", err)
	}
	ref, err := url.Parse(loc)
	if err != nil {
		return "", cmdutil.WrapUserError("invalid Location header", err)
	}

	return base.ResolveReference(ref).String(), nil
}

// parseURIResponse reads a JSON response body and extracts the "uri" field.
func (c *Client) parseURIResponse(body io.Reader) (string, error) {
	var result map[string]string
	if err := json.NewDecoder(body).Decode(&result); err != nil {
		return "", cmdutil.WrapUserError("failed to parse upload response", err)
	}
	uri, ok := result["uri"]
	if !ok {
		return "", cmdutil.NewUserError("upload response missing 'uri' field")
	}
	return uri, nil
}

// logInfo logs an informational message if a logger is configured.
func (c *Client) logInfo(format string, args ...interface{}) {
	if c.logger != nil {
		c.logger.Info(format, args...)
	}
}
