package bbs

import (
	"bytes"
	"errors"
	"io"
	"os"
	"testing"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------- mock sftpClient ----------

type mockSFTPClient struct {
	StatFunc  func(path string) (os.FileInfo, error)
	OpenFunc  func(path string) (io.ReadCloser, error)
	CloseFunc func() error
}

func (m *mockSFTPClient) Stat(path string) (os.FileInfo, error) {
	if m.StatFunc != nil {
		return m.StatFunc(path)
	}
	return nil, errors.New("not implemented")
}

func (m *mockSFTPClient) Open(path string) (io.ReadCloser, error) {
	if m.OpenFunc != nil {
		return m.OpenFunc(path)
	}
	return nil, errors.New("not implemented")
}

func (m *mockSFTPClient) Close() error {
	if m.CloseFunc != nil {
		return m.CloseFunc()
	}
	return nil
}

// ---------- mock fileSystem ----------

type mockFileSystem struct {
	MkdirAllFunc func(path string, perm os.FileMode) error
	CreateFunc   func(path string) (io.WriteCloser, error)
}

func (m *mockFileSystem) MkdirAll(path string, perm os.FileMode) error {
	if m.MkdirAllFunc != nil {
		return m.MkdirAllFunc(path, perm)
	}
	return nil
}

func (m *mockFileSystem) Create(path string) (io.WriteCloser, error) {
	if m.CreateFunc != nil {
		return m.CreateFunc(path)
	}
	return &nopWriteCloser{buf: &bytes.Buffer{}}, nil
}

// nopWriteCloser wraps a bytes.Buffer with a no-op Close.
type nopWriteCloser struct {
	buf *bytes.Buffer
}

func (w *nopWriteCloser) Write(p []byte) (int, error) { return w.buf.Write(p) }
func (w *nopWriteCloser) Close() error                { return nil }

// ---------- mock os.FileInfo ----------

type mockFileInfo struct {
	size int64
}

func (m *mockFileInfo) Name() string       { return "archive.tar" }
func (m *mockFileInfo) Size() int64        { return m.size }
func (m *mockFileInfo) Mode() os.FileMode  { return 0o644 }
func (m *mockFileInfo) ModTime() time.Time { return time.Time{} }
func (m *mockFileInfo) IsDir() bool        { return false }
func (m *mockFileInfo) Sys() interface{}   { return nil }

// ---------- SSHArchiveDownloader tests ----------

func TestSSHDownload_ReturnsDownloadedArchiveFullName(t *testing.T) {
	const exportJobID int64 = 1
	const bbsSharedHome = "/bbs/shared/home"

	expectedSourcePath := "bbs/shared/home/data/migration/export/Bitbucket_export_1.tar"
	var statCalledWith string
	var openCalledWith string
	archiveContent := []byte("archive-content")

	client := &mockSFTPClient{
		StatFunc: func(path string) (os.FileInfo, error) {
			statCalledWith = path
			return &mockFileInfo{size: int64(len(archiveContent))}, nil
		},
		OpenFunc: func(path string) (io.ReadCloser, error) {
			openCalledWith = path
			return io.NopCloser(bytes.NewReader(archiveContent)), nil
		},
	}

	var mkdirCalledWith string
	var createCalledWith string
	writtenBuf := &bytes.Buffer{}
	fs := &mockFileSystem{
		MkdirAllFunc: func(path string, _ os.FileMode) error {
			mkdirCalledWith = path
			return nil
		},
		CreateFunc: func(path string) (io.WriteCloser, error) {
			createCalledWith = path
			return &nopWriteCloser{buf: writtenBuf}, nil
		},
	}

	log := logger.New(false, io.Discard)
	d := newSSHArchiveDownloaderWithClient(log, client, fs)
	d.BbsSharedHomeDirectory = bbsSharedHome

	result, err := d.Download(exportJobID, "target-dir")

	require.NoError(t, err)
	assert.Equal(t, "target-dir/Bitbucket_export_1.tar", result)
	// Verify the source path uses forward slashes and strips the leading /
	// filepath.Join("/bbs/shared/home", "data/migration/export", "Bitbucket_export_1.tar")
	// → "/bbs/shared/home/data/migration/export/Bitbucket_export_1.tar"
	// filepath.ToSlash keeps it the same on Unix.
	assert.Contains(t, statCalledWith, expectedSourcePath)
	assert.Equal(t, statCalledWith, openCalledWith)
	assert.Equal(t, "target-dir", mkdirCalledWith)
	assert.Equal(t, "target-dir/Bitbucket_export_1.tar", createCalledWith)
	assert.Equal(t, archiveContent, writtenBuf.Bytes())
}

func TestSSHDownload_UsesDefaultTargetDirectory(t *testing.T) {
	client := &mockSFTPClient{
		StatFunc: func(_ string) (os.FileInfo, error) {
			return &mockFileInfo{size: 0}, nil
		},
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return io.NopCloser(bytes.NewReader(nil)), nil
		},
	}

	var createCalledWith string
	fs := &mockFileSystem{
		CreateFunc: func(path string) (io.WriteCloser, error) {
			createCalledWith = path
			return &nopWriteCloser{buf: &bytes.Buffer{}}, nil
		},
	}

	log := logger.New(false, io.Discard)
	d := newSSHArchiveDownloaderWithClient(log, client, fs)

	result, err := d.Download(42, "")
	require.NoError(t, err)
	assert.Equal(t, "bbs_archive_downloads/Bitbucket_export_42.tar", result)
	assert.Equal(t, "bbs_archive_downloads/Bitbucket_export_42.tar", createCalledWith)
}

func TestSSHDownload_ThrowsWhenSourceExportArchiveDoesNotExist(t *testing.T) {
	client := &mockSFTPClient{
		StatFunc: func(_ string) (os.FileInfo, error) {
			return nil, errors.New("file not found")
		},
	}

	log := logger.New(false, io.Discard)
	d := newSSHArchiveDownloaderWithClient(log, client, &mockFileSystem{})

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "does not exist")
}

func TestSSHDownload_ThrowsWithHintWhenUsingDefaultSharedHome(t *testing.T) {
	client := &mockSFTPClient{
		StatFunc: func(_ string) (os.FileInfo, error) {
			return nil, errors.New("file not found")
		},
	}

	log := logger.New(false, io.Discard)
	d := newSSHArchiveDownloaderWithClient(log, client, &mockFileSystem{})
	// Default is DefaultBbsSharedHomeDirectoryLinux

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "--bbs-shared-home")
}

func TestSSHDownload_NoHintWhenUsingCustomSharedHome(t *testing.T) {
	client := &mockSFTPClient{
		StatFunc: func(_ string) (os.FileInfo, error) {
			return nil, errors.New("file not found")
		},
	}

	log := logger.New(false, io.Discard)
	d := newSSHArchiveDownloaderWithClient(log, client, &mockFileSystem{})
	d.BbsSharedHomeDirectory = "/custom/path"

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.NotContains(t, ue.Message, "--bbs-shared-home")
}

func TestSSHDownload_CreatesTargetDirectory(t *testing.T) {
	client := &mockSFTPClient{
		StatFunc: func(_ string) (os.FileInfo, error) {
			return &mockFileInfo{size: 0}, nil
		},
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return io.NopCloser(bytes.NewReader(nil)), nil
		},
	}

	mkdirCalled := false
	fs := &mockFileSystem{
		MkdirAllFunc: func(path string, _ os.FileMode) error {
			mkdirCalled = true
			assert.Equal(t, "my-target", path)
			return nil
		},
	}

	log := logger.New(false, io.Discard)
	d := newSSHArchiveDownloaderWithClient(log, client, fs)

	_, err := d.Download(1, "my-target")
	require.NoError(t, err)
	assert.True(t, mkdirCalled, "MkdirAll should have been called")
}
