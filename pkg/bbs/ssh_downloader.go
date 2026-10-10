package bbs

import (
	"fmt"
	"io"
	"os"
	"path/filepath"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

// sftpClient abstracts the SFTP operations needed by SSHArchiveDownloader.
// Consumer-defined interface for testability.
type sftpClient interface {
	Stat(path string) (os.FileInfo, error)
	Open(path string) (io.ReadCloser, error)
	Close() error
}

// SSHArchiveDownloader downloads BBS export archives via SFTP over SSH.
type SSHArchiveDownloader struct {
	client                 sftpClient
	log                    *logger.Logger
	fs                     fileSystem
	BbsSharedHomeDirectory string
}

// NewSSHArchiveDownloader creates a downloader that connects via SSH/SFTP.
// The privateKeyPath must point to an unencrypted PEM private key file.
func NewSSHArchiveDownloader(log *logger.Logger, host string, sshUser string, privateKeyPath string, sshPort int) (*SSHArchiveDownloader, error) {
	client, err := newRealSFTPClient(host, sshUser, privateKeyPath, sshPort)
	if err != nil {
		return nil, err
	}
	return &SSHArchiveDownloader{
		client:                 client,
		log:                    log,
		fs:                     osFileSystem{},
		BbsSharedHomeDirectory: DefaultBbsSharedHomeDirectoryLinux,
	}, nil
}

// newSSHArchiveDownloaderWithClient creates an SSHArchiveDownloader with an injected client (for testing).
func newSSHArchiveDownloaderWithClient(log *logger.Logger, client sftpClient, fs fileSystem) *SSHArchiveDownloader {
	return &SSHArchiveDownloader{
		client:                 client,
		log:                    log,
		fs:                     fs,
		BbsSharedHomeDirectory: DefaultBbsSharedHomeDirectoryLinux,
	}
}

func (d *SSHArchiveDownloader) sourceExportArchiveAbsolutePath(exportJobID int64) string {
	home := d.BbsSharedHomeDirectory
	if home == "" {
		home = DefaultBbsSharedHomeDirectoryLinux
	}
	return SourceExportArchiveAbsolutePath(home, exportJobID)
}

// Download downloads the export archive for the given job ID to targetDirectory.
// Returns the full path of the downloaded file.
func (d *SSHArchiveDownloader) Download(exportJobID int64, targetDirectory string) (string, error) {
	if targetDirectory == "" {
		targetDirectory = DefaultTargetDirectory
	}

	sourcePath := d.sourceExportArchiveAbsolutePath(exportJobID)
	targetPath := filepath.ToSlash(filepath.Join(targetDirectory, ExportArchiveFileName(exportJobID)))

	// Check if source file exists and get its size.
	info, err := d.client.Stat(sourcePath)
	if err != nil {
		hint := ""
		if d.BbsSharedHomeDirectory == DefaultBbsSharedHomeDirectoryLinux || d.BbsSharedHomeDirectory == "" {
			hint = "This most likely means that your Bitbucket instance uses a non-default Bitbucket shared home directory, so we couldn't find your archive. " +
				"You can point the CLI to a non-default shared directory by specifying the --bbs-shared-home option."
		}
		return "", cmdutil.NewUserErrorf(
			"Source export archive (%s) does not exist.%s", sourcePath, hint,
		)
	}

	// Create target directory.
	if err := d.fs.MkdirAll(targetDirectory, 0o755); err != nil {
		return "", fmt.Errorf("create target directory: %w", err)
	}

	// Open remote file.
	remoteFile, err := d.client.Open(sourcePath)
	if err != nil {
		return "", fmt.Errorf("open remote file: %w", err)
	}
	defer remoteFile.Close()

	// Create local file.
	localFile, err := d.fs.Create(targetPath)
	if err != nil {
		return "", fmt.Errorf("create local file: %w", err)
	}
	defer localFile.Close()

	if err := copyWithProgress(remoteFile, localFile.(io.Writer), info.Size(), d.log); err != nil {
		return "", err
	}

	return targetPath, nil
}

// Close releases SSH/SFTP resources.
func (d *SSHArchiveDownloader) Close() error {
	if d.client != nil {
		return d.client.Close()
	}
	return nil
}
