// Package archive provides orchestration for uploading migration archives
// to the appropriate storage backend.
package archive

import (
	"context"
	"fmt"
	"io"

	"github.com/github/gh-gei/pkg/logger"
)

// AzureUploader uploads an archive to Azure Blob Storage.
type AzureUploader interface {
	Upload(ctx context.Context, fileName string, content io.Reader, size int64) (string, error)
}

// AWSUploader uploads an archive to AWS S3.
type AWSUploader interface {
	Upload(ctx context.Context, bucket, key string, data io.Reader) (string, error)
}

// GitHubUploader uploads an archive to GitHub-owned storage.
type GitHubUploader interface {
	Upload(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error)
}

// OrgIDResolver resolves an organization login to its database ID.
type OrgIDResolver interface {
	GetOrganizationDatabaseId(ctx context.Context, org string) (string, error)
}

// Uploader coordinates archive uploads across storage backends.
// Priority follows the C# original: GitHub-owned > AWS S3 > Azure Blob.
type Uploader struct {
	azure         AzureUploader
	aws           AWSUploader
	awsBucket     string
	github        GitHubUploader
	orgIDResolver OrgIDResolver
	logger        *logger.Logger
}

// UploaderOption configures an Uploader.
type UploaderOption func(*Uploader)

// WithAzure configures Azure Blob Storage as an upload backend.
func WithAzure(a AzureUploader) UploaderOption {
	return func(u *Uploader) {
		u.azure = a
	}
}

// WithAWS configures AWS S3 as an upload backend.
func WithAWS(a AWSUploader, bucket string) UploaderOption {
	return func(u *Uploader) {
		u.aws = a
		u.awsBucket = bucket
	}
}

// WithGitHub configures GitHub-owned storage as an upload backend.
func WithGitHub(g GitHubUploader, resolver OrgIDResolver) UploaderOption {
	return func(u *Uploader) {
		u.github = g
		u.orgIDResolver = resolver
	}
}

// WithLogger sets the logger for upload progress messages.
func WithLogger(l *logger.Logger) UploaderOption {
	return func(u *Uploader) {
		u.logger = l
	}
}

// NewUploader creates an Uploader with the given options.
func NewUploader(opts ...UploaderOption) *Uploader {
	u := &Uploader{}
	for _, opt := range opts {
		opt(u)
	}
	return u
}

// Upload uploads the archive to the highest-priority configured backend.
// Priority: GitHub-owned storage > AWS S3 > Azure Blob Storage.
// Returns the URL of the uploaded archive.
func (u *Uploader) Upload(ctx context.Context, targetOrg, fileName string, content io.ReadSeeker, size int64) (string, error) {
	if u.github != nil {
		return u.uploadToGitHub(ctx, targetOrg, fileName, content, size)
	}

	if u.aws != nil {
		return u.uploadToAWS(ctx, fileName, content)
	}

	if u.azure != nil {
		return u.uploadToAzure(ctx, fileName, content, size)
	}

	return "", fmt.Errorf("no upload destination configured; provide Azure, AWS, or GitHub-owned storage credentials")
}

func (u *Uploader) uploadToGitHub(ctx context.Context, targetOrg, fileName string, content io.ReadSeeker, size int64) (string, error) {
	u.logInfo("Uploading archive %s to GitHub-owned storage", fileName)

	orgDatabaseID, err := u.orgIDResolver.GetOrganizationDatabaseId(ctx, targetOrg)
	if err != nil {
		return "", fmt.Errorf("resolving org database ID for %q: %w", targetOrg, err)
	}

	url, err := u.github.Upload(ctx, orgDatabaseID, fileName, content, size)
	if err != nil {
		return "", err
	}

	u.logInfo("Upload complete")
	return url, nil
}

func (u *Uploader) uploadToAWS(ctx context.Context, fileName string, content io.Reader) (string, error) {
	u.logInfo("Uploading archive %s to AWS", fileName)

	url, err := u.aws.Upload(ctx, u.awsBucket, fileName, content)
	if err != nil {
		return "", err
	}

	u.logInfo("Upload complete")
	return url, nil
}

func (u *Uploader) uploadToAzure(ctx context.Context, fileName string, content io.Reader, size int64) (string, error) {
	u.logInfo("Uploading archive %s to Azure Blob Storage", fileName)

	url, err := u.azure.Upload(ctx, fileName, content, size)
	if err != nil {
		return "", err
	}

	u.logInfo("Upload complete")
	return url, nil
}

func (u *Uploader) logInfo(format string, args ...interface{}) {
	if u.logger != nil {
		u.logger.Info(format, args...)
	}
}
