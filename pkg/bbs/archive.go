package bbs

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sync"
	"time"

	"github.com/github/gh-gei/pkg/logger"
)

// Archive download constants matching C# IBbsArchiveDownloader and BbsSettings.
const (
	ExportArchiveSourceDirectory         = "data/migration/export"
	DefaultTargetDirectory               = "bbs_archive_downloads"
	DefaultBbsSharedHomeDirectoryLinux   = "/var/atlassian/application-data/bitbucket/shared"
	DefaultBbsSharedHomeDirectoryWindows = `c$\atlassian\applicationdata\bitbucket\shared`
	downloadProgressReportInterval       = 10 * time.Second
)

// ExportArchiveFileName returns the archive filename for an export job.
func ExportArchiveFileName(exportJobID int64) string {
	return fmt.Sprintf("Bitbucket_export_%d.tar", exportJobID)
}

// SourceExportArchiveAbsolutePath returns the full path to the export archive on the BBS server.
func SourceExportArchiveAbsolutePath(bbsSharedHome string, exportJobID int64) string {
	return filepath.ToSlash(filepath.Join(bbsSharedHome, ExportArchiveSourceDirectory, ExportArchiveFileName(exportJobID)))
}

// fileSystem abstracts filesystem operations for testability.
type fileSystem interface {
	MkdirAll(path string, perm os.FileMode) error
	Create(path string) (io.WriteCloser, error)
}

// osFileSystem is the default fileSystem implementation using the real OS.
type osFileSystem struct{}

func (osFileSystem) MkdirAll(path string, perm os.FileMode) error {
	return os.MkdirAll(path, perm)
}

func (osFileSystem) Create(path string) (io.WriteCloser, error) {
	return os.Create(path)
}

// progressLogger tracks download progress with rate-limited log output.
type progressLogger struct {
	log              *logger.Logger
	mu               sync.Mutex
	nextProgressTime time.Time
}

func newProgressLogger(log *logger.Logger) *progressLogger {
	return &progressLogger{
		log:              log,
		nextProgressTime: time.Now(),
	}
}

func (p *progressLogger) logProgress(downloadedBytes, totalBytes int64) {
	p.mu.Lock()
	defer p.mu.Unlock()

	if time.Now().Before(p.nextProgressTime) {
		return
	}

	if totalBytes > 0 {
		p.log.Info(
			"Archive download in progress, %s out of %s (%s) completed...",
			logFriendlySize(downloadedBytes),
			logFriendlySize(totalBytes),
			percentage(downloadedBytes, totalBytes),
		)
	} else {
		p.log.Info("Archive download in progress, %s completed...", logFriendlySize(downloadedBytes))
	}

	p.nextProgressTime = p.nextProgressTime.Add(downloadProgressReportInterval)
}

func percentage(downloaded, total int64) string {
	if total == 0 {
		return "unknown%"
	}
	pct := int(float64(downloaded) * 100.0 / float64(total))
	return fmt.Sprintf("%d%%", pct)
}

func logFriendlySize(size int64) string {
	const (
		kilobyte = 1024
		megabyte = 1024 * kilobyte
		gigabyte = 1024 * megabyte
	)

	switch {
	case size < kilobyte:
		return fmt.Sprintf("%d bytes", size)
	case size < megabyte:
		return fmt.Sprintf("%.0f KB", float64(size)/float64(kilobyte))
	case size < gigabyte:
		return fmt.Sprintf("%.0f MB", float64(size)/float64(megabyte))
	default:
		return fmt.Sprintf("%.2f GB", float64(size)/float64(gigabyte))
	}
}

// copyWithProgress copies from src to dst, reporting download progress.
func copyWithProgress(src io.Reader, dst io.Writer, totalSize int64, log *logger.Logger) error {
	progress := newProgressLogger(log)
	buf := make([]byte, 64*1024)
	var downloaded int64
	for {
		n, readErr := src.Read(buf)
		if n > 0 {
			if _, writeErr := dst.Write(buf[:n]); writeErr != nil {
				return fmt.Errorf("write to local file: %w", writeErr)
			}
			downloaded += int64(n)
			progress.logProgress(downloaded, totalSize)
		}
		if readErr == io.EOF {
			break
		}
		if readErr != nil {
			return fmt.Errorf("read from remote: %w", readErr)
		}
	}
	return nil
}
