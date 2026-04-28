package bbs

import (
	"fmt"
	"io"
	"path/filepath"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

// smbShare abstracts SMB share operations for testability.
type smbShare interface {
	Open(name string) (io.ReadCloser, error)
	Stat(name string) (int64, error)
}

// smbConnector abstracts SMB connection and authentication for testability.
type smbConnector interface {
	Connect(host string) error
	Login(user, password, domain string) error
	Mount(shareName string) (smbShare, error)
	Logoff() error
	Close() error
}

// SMBArchiveDownloader downloads BBS export archives via SMB.
type SMBArchiveDownloader struct {
	connector              smbConnector
	log                    *logger.Logger
	fs                     fileSystem
	host                   string
	smbUser                string
	smbPassword            string
	domainName             string
	BbsSharedHomeDirectory string
}

// NewSMBArchiveDownloader creates an SMB-based archive downloader.
func NewSMBArchiveDownloader(
	log *logger.Logger,
	host, smbUser, smbPassword, domainName string,
) *SMBArchiveDownloader {
	return &SMBArchiveDownloader{
		connector:              &realSMBConnector{},
		log:                    log,
		fs:                     osFileSystem{},
		host:                   host,
		smbUser:                smbUser,
		smbPassword:            smbPassword,
		domainName:             domainName,
		BbsSharedHomeDirectory: DefaultBbsSharedHomeDirectoryWindows,
	}
}

// newSMBArchiveDownloaderWithDeps creates an SMBArchiveDownloader with injected deps (for testing).
func newSMBArchiveDownloaderWithDeps(
	log *logger.Logger,
	connector smbConnector,
	fs fileSystem,
	host, smbUser, smbPassword, domainName string,
) *SMBArchiveDownloader {
	return &SMBArchiveDownloader{
		connector:              connector,
		log:                    log,
		fs:                     fs,
		host:                   host,
		smbUser:                smbUser,
		smbPassword:            smbPassword,
		domainName:             domainName,
		BbsSharedHomeDirectory: DefaultBbsSharedHomeDirectoryWindows,
	}
}

func (d *SMBArchiveDownloader) sourceExportArchiveAbsolutePath(exportJobID int64) string {
	home := d.BbsSharedHomeDirectory
	if home == "" {
		home = DefaultBbsSharedHomeDirectoryWindows
	}
	// Build the Windows-style path for SMB.
	return toWindowsPath(filepath.Join(home, ExportArchiveSourceDirectory, ExportArchiveFileName(exportJobID)))
}

// toWindowsPath converts a path to Windows-style backslashes.
func toWindowsPath(p string) string {
	return strings.ReplaceAll(p, "/", `\`)
}

// splitSMBPath splits a Windows-style SMB path into share name and path within the share.
// e.g. `c$\atlassian\...` → ("c$", `atlassian\...`, nil)
func splitSMBPath(sourcePath string) (share, pathInShare string, err error) {
	backslashIdx := strings.Index(sourcePath, `\`)
	if backslashIdx < 0 {
		return "", "", cmdutil.NewUserErrorf("invalid SMB source path: %s", sourcePath)
	}
	return sourcePath[:backslashIdx], sourcePath[backslashIdx+1:], nil
}

// connectAndMount establishes the SMB session and mounts the share.
// The caller is responsible for calling the returned cleanup function.
func (d *SMBArchiveDownloader) connectAndMount(share string) (smbShare, func(), error) {
	if err := d.connector.Connect(d.host); err != nil {
		return nil, nil, cmdutil.NewUserErrorf("Failed to connect to SMB host '%s'. %v", d.host, err)
	}

	if err := d.connector.Login(d.smbUser, d.smbPassword, d.domainName); err != nil {
		_ = d.connector.Close()
		return nil, nil, cmdutil.NewUserErrorf("Failed to authenticate to SMB host '%s' as user '%s'. %v", d.host, d.smbUser, err)
	}

	mountedShare, err := d.connector.Mount(share)
	if err != nil {
		_ = d.connector.Logoff()
		_ = d.connector.Close()
		return nil, nil, cmdutil.NewUserErrorf("Failed to connect to SMB share '%s' on host '%s'. %v", share, d.host, err)
	}

	cleanup := func() {
		_ = d.connector.Logoff()
		_ = d.connector.Close()
	}
	return mountedShare, cleanup, nil
}

// openRemoteArchive opens the export archive on the SMB share, returning it with its size.
func (d *SMBArchiveDownloader) openRemoteArchive(mountedShare smbShare, pathInShare, sourcePath string) (io.ReadCloser, int64, error) {
	totalSize, _ := mountedShare.Stat(pathInShare)

	remoteFile, err := mountedShare.Open(pathInShare)
	if err != nil {
		hint := ""
		if d.BbsSharedHomeDirectory == DefaultBbsSharedHomeDirectoryWindows || d.BbsSharedHomeDirectory == "" {
			hint = "This most likely means that your Bitbucket instance uses a non-default Bitbucket shared home directory, so we couldn't find your archive. " +
				"You can point the CLI to a non-default shared directory by specifying the --bbs-shared-home option."
		}
		return nil, 0, cmdutil.NewUserErrorf(
			"Source export archive (%s) does not exist.%s", sourcePath, hint,
		)
	}
	return remoteFile, totalSize, nil
}

// Download downloads the export archive for the given job ID via SMB.
// Returns the full path of the downloaded file.
func (d *SMBArchiveDownloader) Download(exportJobID int64, targetDirectory string) (string, error) {
	if targetDirectory == "" {
		targetDirectory = DefaultTargetDirectory
	}

	sourcePath := d.sourceExportArchiveAbsolutePath(exportJobID)
	targetPath := filepath.ToSlash(filepath.Join(targetDirectory, ExportArchiveFileName(exportJobID)))

	share, pathInShare, err := splitSMBPath(sourcePath)
	if err != nil {
		return "", err
	}

	// Create target directory and file.
	if err := d.fs.MkdirAll(targetDirectory, 0o755); err != nil {
		return "", fmt.Errorf("create target directory: %w", err)
	}
	localFile, err := d.fs.Create(targetPath)
	if err != nil {
		return "", fmt.Errorf("create local file: %w", err)
	}
	defer localFile.Close()

	// Connect → Login → Mount → Read → cleanup.
	mountedShare, cleanup, err := d.connectAndMount(share)
	if err != nil {
		return "", err
	}
	defer cleanup()

	remoteFile, totalSize, err := d.openRemoteArchive(mountedShare, pathInShare, sourcePath)
	if err != nil {
		return "", err
	}
	defer remoteFile.Close()

	if err := copyWithProgress(remoteFile, localFile.(io.Writer), totalSize, d.log); err != nil {
		return "", err
	}

	return targetPath, nil
}
