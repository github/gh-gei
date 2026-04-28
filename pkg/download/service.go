// Package download provides HTTP download functionality.
package download

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"os"
	"time"
)

const defaultTimeout = 1 * time.Hour

// Service downloads files over HTTP.
type Service struct {
	client *http.Client
}

// New creates a new download service with the given HTTP client.
// If client is nil, a default client with 1-hour timeout is used.
func New(client *http.Client) *Service {
	if client == nil {
		client = &http.Client{Timeout: defaultTimeout}
	}
	return &Service{client: client}
}

// DownloadToFile downloads content from url and writes it to the given destPath.
func (s *Service) DownloadToFile(ctx context.Context, url, destPath string) error {
	resp, err := s.doGet(ctx, url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("download failed: HTTP %d", resp.StatusCode)
	}

	f, err := os.Create(destPath)
	if err != nil {
		return fmt.Errorf("creating file %s: %w", destPath, err)
	}

	if _, err := io.Copy(f, resp.Body); err != nil {
		f.Close()
		os.Remove(destPath) // clean up partial file
		return fmt.Errorf("writing to file %s: %w", destPath, err)
	}

	// Close explicitly to catch flush/sync errors.
	if err := f.Close(); err != nil {
		return fmt.Errorf("closing file %s: %w", destPath, err)
	}
	return nil
}

// DownloadToBytes downloads content from url and returns it as a byte slice.
func (s *Service) DownloadToBytes(ctx context.Context, url string) ([]byte, error) {
	resp, err := s.doGet(ctx, url)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("download failed: HTTP %d", resp.StatusCode)
	}

	data, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, fmt.Errorf("reading response body: %w", err)
	}
	return data, nil
}

func (s *Service) doGet(ctx context.Context, url string) (*http.Response, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return nil, fmt.Errorf("creating request for %s: %w", url, err)
	}
	resp, err := s.client.Do(req)
	if err != nil {
		return nil, fmt.Errorf("GET %s: %w", url, err)
	}
	return resp, nil
}
