// Package aws provides an S3 client for uploading migration archives to AWS.
package aws

import (
	"context"
	"errors"
	"fmt"
	"io"
	"os"
	"sync"
	"time"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/credentials"
	"github.com/aws/aws-sdk-go-v2/service/s3"
)

const (
	// presignExpiry is the duration for pre-signed URLs (48 hours, matching C# AUTHORIZATION_TIMEOUT_IN_HOURS).
	presignExpiry = 48 * time.Hour

	// progressReportInterval is the minimum time between progress log messages.
	progressReportInterval = 10 * time.Second
)

// S3Uploader abstracts S3 PutObject for testability.
type S3Uploader interface {
	PutObject(ctx context.Context, bucket, key string, body io.Reader) error
}

// S3Presigner abstracts S3 presigned URL generation for testability.
type S3Presigner interface {
	PresignGetObject(ctx context.Context, bucket, key string, expires time.Duration) (string, error)
}

// ProgressLogger is the subset of logger used for upload progress reporting.
type ProgressLogger interface {
	LogInfo(format string, args ...interface{})
}

// Client provides S3 upload operations with pre-signed URL generation.
type Client struct {
	uploader  S3Uploader
	presigner S3Presigner
	logger    ProgressLogger

	mu               sync.Mutex
	nextProgressTime time.Time
}

// Option configures Client construction.
type Option func(*clientConfig)

type clientConfig struct {
	region       string
	sessionToken string
	logger       ProgressLogger
}

// WithRegion sets the AWS region.
func WithRegion(region string) Option {
	return func(c *clientConfig) {
		c.region = region
	}
}

// WithSessionToken sets an AWS session token for temporary credentials.
func WithSessionToken(token string) Option {
	return func(c *clientConfig) {
		c.sessionToken = token
	}
}

// WithLogger sets the progress logger.
func WithLogger(l ProgressLogger) Option {
	return func(c *clientConfig) {
		c.logger = l
	}
}

// NewClient creates a new AWS S3 Client using the provided credentials.
func NewClient(accessKey, secretKey string, opts ...Option) (*Client, error) {
	cfg := &clientConfig{}
	for _, o := range opts {
		o(cfg)
	}

	var creds aws.CredentialsProvider
	if cfg.sessionToken != "" {
		creds = credentials.NewStaticCredentialsProvider(accessKey, secretKey, cfg.sessionToken)
	} else {
		creds = credentials.NewStaticCredentialsProvider(accessKey, secretKey, "")
	}

	s3Client := s3.New(s3.Options{
		Credentials: creds,
		Region:      cfg.region,
	})
	presignClient := s3.NewPresignClient(s3Client)

	return &Client{
		uploader:  &sdkUploader{client: s3Client},
		presigner: &sdkPresigner{client: presignClient},
		logger:    cfg.logger,
	}, nil
}

// NewClientFromInterfaces creates a Client from pre-built interfaces (for testing).
func NewClientFromInterfaces(uploader S3Uploader, presigner S3Presigner, logger ProgressLogger) *Client {
	return &Client{
		uploader:  uploader,
		presigner: presigner,
		logger:    logger,
	}
}

// Upload uploads data from an io.Reader to S3 and returns a pre-signed URL.
func (c *Client) Upload(ctx context.Context, bucket, key string, data io.Reader) (string, error) {
	if err := c.uploader.PutObject(ctx, bucket, key, data); err != nil {
		if ctx.Err() != nil || isTimeout(err) {
			return "", fmt.Errorf("upload to AWS timed out: %w", err)
		}
		return "", fmt.Errorf("failed to upload to S3: %w", err)
	}

	url, err := c.presigner.PresignGetObject(ctx, bucket, key, presignExpiry)
	if err != nil {
		return "", fmt.Errorf("failed to generate pre-signed URL: %w", err)
	}

	return url, nil
}

// UploadFile opens a file and uploads it to S3, returning a pre-signed URL.
// Catches timeout/context errors and wraps them with a user-friendly message
// (matching C# behavior for TaskCanceledException/TimeoutException).
func (c *Client) UploadFile(ctx context.Context, bucket, key, filePath string) (string, error) {
	f, err := os.Open(filePath)
	if err != nil {
		return "", fmt.Errorf("failed to open file %q: %w", filePath, err)
	}
	defer f.Close()

	// Wrap with progress reporting if logger is available
	var reader io.Reader = f
	if c.logger != nil {
		info, statErr := f.Stat()
		if statErr == nil && info.Size() > 0 {
			reader = c.newProgressReader(f, info.Size())
		}
	}

	url, err := c.Upload(ctx, bucket, key, reader)
	if err != nil {
		if isTimeout(err) {
			return "", fmt.Errorf("upload of archive %q to AWS timed out: %w", filePath, err)
		}
		return "", err
	}
	return url, nil
}

// isTimeout checks whether an error represents a timeout.
func isTimeout(err error) bool {
	if errors.Is(err, context.DeadlineExceeded) {
		return true
	}
	type timeouter interface {
		Timeout() bool
	}
	var te timeouter
	if errors.As(err, &te) {
		return te.Timeout()
	}
	return false
}

// --- Progress reader ---

type progressReader struct {
	reader      io.Reader
	total       int64
	transferred int64
	client      *Client
}

func (c *Client) newProgressReader(r io.Reader, total int64) *progressReader {
	return &progressReader{
		reader: r,
		total:  total,
		client: c,
	}
}

func (pr *progressReader) Read(p []byte) (int, error) {
	n, err := pr.reader.Read(p)
	pr.transferred += int64(n)
	pr.client.logProgress(pr.transferred, pr.total)
	return n, err
}

func (c *Client) logProgress(transferred, total int64) {
	if c.logger == nil {
		return
	}

	c.mu.Lock()
	now := time.Now()
	if now.Before(c.nextProgressTime) {
		c.mu.Unlock()
		return
	}
	c.nextProgressTime = now.Add(progressReportInterval)
	c.mu.Unlock()

	if total > 0 {
		percent := int(float64(transferred) / float64(total) * 100)
		c.logger.LogInfo(
			"Archive upload in progress, %s out of %s (%d%%) completed...",
			formatBytes(transferred), formatBytes(total), percent,
		)
	} else {
		c.logger.LogInfo("Archive upload in progress...")
	}
}

// formatBytes returns a human-friendly size string.
func formatBytes(b int64) string {
	const (
		kb = 1024
		mb = kb * 1024
		gb = mb * 1024
	)
	switch {
	case b >= gb:
		return fmt.Sprintf("%.2f GB", float64(b)/float64(gb))
	case b >= mb:
		return fmt.Sprintf("%.2f MB", float64(b)/float64(mb))
	case b >= kb:
		return fmt.Sprintf("%.2f KB", float64(b)/float64(kb))
	default:
		return fmt.Sprintf("%d bytes", b)
	}
}

// --- SDK adapter implementations ---

type sdkUploader struct {
	client *s3.Client
}

func (u *sdkUploader) PutObject(ctx context.Context, bucket, key string, body io.Reader) error {
	_, err := u.client.PutObject(ctx, &s3.PutObjectInput{
		Bucket: &bucket,
		Key:    &key,
		Body:   body,
	})
	return err
}

type sdkPresigner struct {
	client *s3.PresignClient
}

func (p *sdkPresigner) PresignGetObject(ctx context.Context, bucket, key string, expires time.Duration) (string, error) {
	req, err := p.client.PresignGetObject(ctx, &s3.GetObjectInput{
		Bucket: &bucket,
		Key:    &key,
	}, s3.WithPresignExpires(expires))
	if err != nil {
		return "", err
	}
	return req.URL, nil
}
