package filesystem

import (
	"io"
	"os"
	"path/filepath"
)

// Provider provides filesystem operations
// Equivalent to C# FileSystemProvider
type Provider struct{}

// New creates a new filesystem provider
func New() *Provider {
	return &Provider{}
}

// ReadAllText reads all text from a file
func (p *Provider) ReadAllText(path string) (string, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return "", err
	}
	return string(data), nil
}

// ReadAllBytes reads all bytes from a file
func (p *Provider) ReadAllBytes(path string) ([]byte, error) {
	return os.ReadFile(path)
}

// WriteAllText writes text to a file
func (p *Provider) WriteAllText(path, content string) error {
	return os.WriteFile(path, []byte(content), 0o600)
}

// WriteAllBytes writes bytes to a file
func (p *Provider) WriteAllBytes(path string, data []byte) error {
	return os.WriteFile(path, data, 0o600)
}

// FileExists checks if a file exists
func (p *Provider) FileExists(path string) bool {
	info, err := os.Stat(path)
	if err != nil {
		return false
	}
	return !info.IsDir()
}

// DirectoryExists checks if a directory exists
func (p *Provider) DirectoryExists(path string) bool {
	info, err := os.Stat(path)
	if err != nil {
		return false
	}
	return info.IsDir()
}

// CreateDirectory creates a directory and all parent directories
func (p *Provider) CreateDirectory(path string) error {
	return os.MkdirAll(path, 0o755)
}

// DeleteFile deletes a file
func (p *Provider) DeleteFile(path string) error {
	return os.Remove(path)
}

// DeleteDirectory deletes a directory and all its contents
func (p *Provider) DeleteDirectory(path string) error {
	return os.RemoveAll(path)
}

// GetTempPath returns the system temp directory
func (p *Provider) GetTempPath() string {
	return os.TempDir()
}

// GetTempFileName creates a temporary file and returns its path
func (p *Provider) GetTempFileName() (string, error) {
	file, err := os.CreateTemp("", "gei-*.tmp")
	if err != nil {
		return "", err
	}
	defer file.Close()
	return file.Name(), nil
}

// CopyFile copies a file from source to destination
func (p *Provider) CopyFile(src, dst string) error {
	source, err := os.Open(src)
	if err != nil {
		return err
	}
	defer source.Close()

	destination, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer destination.Close()

	_, err = io.Copy(destination, source)
	return err
}

// GetFileSize returns the size of a file in bytes
func (p *Provider) GetFileSize(path string) (int64, error) {
	info, err := os.Stat(path)
	if err != nil {
		return 0, err
	}
	return info.Size(), nil
}

// GetFileName returns the filename from a path
func (p *Provider) GetFileName(path string) string {
	return filepath.Base(path)
}

// GetDirectoryName returns the directory name from a path
func (p *Provider) GetDirectoryName(path string) string {
	return filepath.Dir(path)
}

// OpenRead opens a file for reading and returns an io.ReadSeekCloser and the file size
func (p *Provider) OpenRead(path string) (io.ReadSeekCloser, int64, error) {
	file, err := os.Open(path)
	if err != nil {
		return nil, 0, err
	}
	info, err := file.Stat()
	if err != nil {
		file.Close()
		return nil, 0, err
	}
	return file, info.Size(), nil
}

// DeleteIfExists deletes a file if it exists; no error if the file does not exist
func (p *Provider) DeleteIfExists(path string) error {
	err := os.Remove(path)
	if err != nil && !os.IsNotExist(err) {
		return err
	}
	return nil
}

// Combine joins path elements
func (p *Provider) Combine(paths ...string) string {
	return filepath.Join(paths...)
}
