package bbs

import (
	"bytes"
	"errors"
	"io"
	"os"
	"testing"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------- mock smbConnector ----------

type mockSMBConnector struct {
	ConnectFunc func(host string) error
	LoginFunc   func(user, password, domain string) error
	MountFunc   func(shareName string) (smbShare, error)
	LogoffFunc  func() error
	CloseFunc   func() error
}

func (m *mockSMBConnector) Connect(host string) error {
	if m.ConnectFunc != nil {
		return m.ConnectFunc(host)
	}
	return nil
}

func (m *mockSMBConnector) Login(user, password, domain string) error {
	if m.LoginFunc != nil {
		return m.LoginFunc(user, password, domain)
	}
	return nil
}

func (m *mockSMBConnector) Mount(shareName string) (smbShare, error) {
	if m.MountFunc != nil {
		return m.MountFunc(shareName)
	}
	return nil, errors.New("not implemented")
}

func (m *mockSMBConnector) Logoff() error {
	if m.LogoffFunc != nil {
		return m.LogoffFunc()
	}
	return nil
}

func (m *mockSMBConnector) Close() error {
	if m.CloseFunc != nil {
		return m.CloseFunc()
	}
	return nil
}

// ---------- mock smbShare ----------

type mockSMBShare struct {
	OpenFunc func(name string) (io.ReadCloser, error)
	StatFunc func(name string) (int64, error)
}

func (m *mockSMBShare) Open(name string) (io.ReadCloser, error) {
	if m.OpenFunc != nil {
		return m.OpenFunc(name)
	}
	return nil, errors.New("not implemented")
}

func (m *mockSMBShare) Stat(name string) (int64, error) {
	if m.StatFunc != nil {
		return m.StatFunc(name)
	}
	return 0, nil
}

// ---------- SMBArchiveDownloader tests ----------

func TestSMBDownload_ReturnsDownloadedArchiveFullName(t *testing.T) {
	const exportJobID int64 = 1

	archiveContent := []byte("smb-archive-content")
	var connectHost string
	var loginUser, loginPassword, loginDomain string
	var mountShare string
	var openPath string

	writtenBuf := &bytes.Buffer{}

	share := &mockSMBShare{
		StatFunc: func(_ string) (int64, error) {
			return int64(len(archiveContent)), nil
		},
		OpenFunc: func(name string) (io.ReadCloser, error) {
			openPath = name
			return io.NopCloser(bytes.NewReader(archiveContent)), nil
		},
	}

	connector := &mockSMBConnector{
		ConnectFunc: func(host string) error {
			connectHost = host
			return nil
		},
		LoginFunc: func(user, password, domain string) error {
			loginUser = user
			loginPassword = password
			loginDomain = domain
			return nil
		},
		MountFunc: func(shareName string) (smbShare, error) {
			mountShare = shareName
			return share, nil
		},
	}

	fs := &mockFileSystem{
		CreateFunc: func(_ string) (io.WriteCloser, error) {
			return &nopWriteCloser{buf: writtenBuf}, nil
		},
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, fs, "smb-host", "smb-user", "smb-pass", "DOMAIN")

	result, err := d.Download(exportJobID, "target-dir")

	require.NoError(t, err)
	assert.Equal(t, "target-dir/Bitbucket_export_1.tar", result)
	assert.Equal(t, "smb-host", connectHost)
	assert.Equal(t, "smb-user", loginUser)
	assert.Equal(t, "smb-pass", loginPassword)
	assert.Equal(t, "DOMAIN", loginDomain)
	assert.Equal(t, "c$", mountShare)
	// The path after the share should be the rest of the Windows path.
	assert.Contains(t, openPath, `atlassian\applicationdata\bitbucket\shared\data\migration\export\Bitbucket_export_1.tar`)
	assert.Equal(t, archiveContent, writtenBuf.Bytes())
}

func TestSMBDownload_UsesDefaultTargetDirectory(t *testing.T) {
	archiveContent := []byte("data")

	share := &mockSMBShare{
		StatFunc: func(_ string) (int64, error) { return int64(len(archiveContent)), nil },
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return io.NopCloser(bytes.NewReader(archiveContent)), nil
		},
	}

	connector := &mockSMBConnector{
		MountFunc: func(_ string) (smbShare, error) { return share, nil },
	}

	var createCalledWith string
	fs := &mockFileSystem{
		CreateFunc: func(path string) (io.WriteCloser, error) {
			createCalledWith = path
			return &nopWriteCloser{buf: &bytes.Buffer{}}, nil
		},
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, fs, "host", "user", "pass", "")

	result, err := d.Download(42, "")
	require.NoError(t, err)
	assert.Equal(t, "bbs_archive_downloads/Bitbucket_export_42.tar", result)
	assert.Equal(t, "bbs_archive_downloads/Bitbucket_export_42.tar", createCalledWith)
}

func TestSMBDownload_ThrowsWhenCannotConnectToHost(t *testing.T) {
	connector := &mockSMBConnector{
		ConnectFunc: func(_ string) error {
			return errors.New("connection refused")
		},
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, &mockFileSystem{}, "bad-host", "user", "pass", "")

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "Failed to connect")
	assert.Contains(t, ue.Message, "bad-host")
}

func TestSMBDownload_ThrowsWhenCannotLogin(t *testing.T) {
	connector := &mockSMBConnector{
		LoginFunc: func(_, _, _ string) error {
			return errors.New("invalid credentials")
		},
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, &mockFileSystem{}, "host", "bad-user", "pass", "")

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "Failed to authenticate")
	assert.Contains(t, ue.Message, "bad-user")
}

func TestSMBDownload_ThrowsWhenCannotConnectToShare(t *testing.T) {
	connector := &mockSMBConnector{
		MountFunc: func(_ string) (smbShare, error) {
			return nil, errors.New("share not found")
		},
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, &mockFileSystem{}, "host", "user", "pass", "")

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "Failed to connect to SMB share")
}

func TestSMBDownload_ThrowsWhenSourceExportArchiveDoesNotExist(t *testing.T) {
	share := &mockSMBShare{
		StatFunc: func(_ string) (int64, error) { return 0, nil },
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return nil, errors.New("file not found")
		},
	}

	connector := &mockSMBConnector{
		MountFunc: func(_ string) (smbShare, error) { return share, nil },
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, &mockFileSystem{}, "host", "user", "pass", "")

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "does not exist")
}

func TestSMBDownload_ThrowsWithHintWhenUsingDefaultSharedHome(t *testing.T) {
	share := &mockSMBShare{
		StatFunc: func(_ string) (int64, error) { return 0, nil },
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return nil, errors.New("file not found")
		},
	}

	connector := &mockSMBConnector{
		MountFunc: func(_ string) (smbShare, error) { return share, nil },
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, &mockFileSystem{}, "host", "user", "pass", "")
	// Default is DefaultBbsSharedHomeDirectoryWindows

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.Contains(t, ue.Message, "--bbs-shared-home")
}

func TestSMBDownload_NoHintWhenUsingCustomSharedHome(t *testing.T) {
	share := &mockSMBShare{
		StatFunc: func(_ string) (int64, error) { return 0, nil },
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return nil, errors.New("file not found")
		},
	}

	connector := &mockSMBConnector{
		MountFunc: func(_ string) (smbShare, error) { return share, nil },
	}

	log := logger.New(false, io.Discard)
	d := newSMBArchiveDownloaderWithDeps(log, connector, &mockFileSystem{}, "host", "user", "pass", "")
	d.BbsSharedHomeDirectory = `custom$\path`

	_, err := d.Download(1, "target-dir")

	require.Error(t, err)
	var ue *cmdutil.UserError
	require.True(t, errors.As(err, &ue))
	assert.NotContains(t, ue.Message, "--bbs-shared-home")
}

func TestSMBDownload_CreatesTargetDirectory(t *testing.T) {
	archiveContent := []byte("data")

	share := &mockSMBShare{
		StatFunc: func(_ string) (int64, error) { return int64(len(archiveContent)), nil },
		OpenFunc: func(_ string) (io.ReadCloser, error) {
			return io.NopCloser(bytes.NewReader(archiveContent)), nil
		},
	}

	connector := &mockSMBConnector{
		MountFunc: func(_ string) (smbShare, error) { return share, nil },
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
	d := newSMBArchiveDownloaderWithDeps(log, connector, fs, "host", "user", "pass", "")

	_, err := d.Download(1, "my-target")
	require.NoError(t, err)
	assert.True(t, mkdirCalled, "MkdirAll should have been called")
}
