package http

import (
	"bytes"
	"context"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"time"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/retry"
)

// Client is a shared HTTP client with retry logic
type Client struct {
	httpClient  *http.Client
	retryPolicy *retry.Policy
	logger      *logger.Logger
}

// Config contains configuration for the HTTP client
type Config struct {
	Timeout       time.Duration
	RetryAttempts int
	NoSSLVerify   bool
}

// DefaultConfig returns a Config with sensible defaults
func DefaultConfig() Config {
	return Config{
		Timeout:       30 * time.Second,
		RetryAttempts: 3,
		NoSSLVerify:   false,
	}
}

// NewClient creates a new HTTP client with the given configuration
func NewClient(cfg Config, log *logger.Logger) *Client {
	transport := &http.Transport{
		TLSClientConfig: &tls.Config{
			InsecureSkipVerify: cfg.NoSSLVerify,
		},
	}

	httpClient := &http.Client{
		Timeout:   cfg.Timeout,
		Transport: transport,
	}

	retryPolicy := retry.New(
		retry.WithMaxAttempts(uint(cfg.RetryAttempts)),
		retry.WithDelay(1*time.Second),
		retry.WithMaxDelay(30*time.Second),
	)

	return &Client{
		httpClient:  httpClient,
		retryPolicy: retryPolicy,
		logger:      log,
	}
}

// Get performs an HTTP GET request with retry logic
func (c *Client) Get(ctx context.Context, url string, headers map[string]string) ([]byte, error) {
	var responseBody []byte

	err := c.retryPolicy.Execute(ctx, func() error {
		req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
		if err != nil {
			return fmt.Errorf("failed to create request: %w", err)
		}

		for key, value := range headers {
			req.Header.Set(key, value)
		}

		c.logger.Debug("HTTP GET: %s", url)

		resp, err := c.httpClient.Do(req)
		if err != nil {
			return fmt.Errorf("request failed: %w", err)
		}
		defer resp.Body.Close()

		body, err := io.ReadAll(resp.Body)
		if err != nil {
			return fmt.Errorf("failed to read response body: %w", err)
		}

		if resp.StatusCode < 200 || resp.StatusCode >= 300 {
			return fmt.Errorf("HTTP %d: %s", resp.StatusCode, string(body))
		}

		responseBody = body
		return nil
	})

	if err != nil {
		return nil, err
	}

	return responseBody, nil
}

// Post performs an HTTP POST request with retry logic
func (c *Client) Post(ctx context.Context, url string, body []byte, headers map[string]string) ([]byte, error) {
	var responseBody []byte

	err := c.retryPolicy.Execute(ctx, func() error {
		req, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(body))
		if err != nil {
			return fmt.Errorf("failed to create request: %w", err)
		}

		for key, value := range headers {
			req.Header.Set(key, value)
		}

		// Set default Content-Type if not provided
		if req.Header.Get("Content-Type") == "" {
			req.Header.Set("Content-Type", "application/json")
		}

		c.logger.Debug("HTTP POST: %s", url)

		resp, err := c.httpClient.Do(req)
		if err != nil {
			return fmt.Errorf("request failed: %w", err)
		}
		defer resp.Body.Close()

		respBody, err := io.ReadAll(resp.Body)
		if err != nil {
			return fmt.Errorf("failed to read response body: %w", err)
		}

		if resp.StatusCode < 200 || resp.StatusCode >= 300 {
			return fmt.Errorf("HTTP %d: %s", resp.StatusCode, string(respBody))
		}

		responseBody = respBody
		return nil
	})

	if err != nil {
		return nil, err
	}

	return responseBody, nil
}

// Put performs an HTTP PUT request with retry logic
func (c *Client) Put(ctx context.Context, url string, body []byte, headers map[string]string) ([]byte, error) {
	var responseBody []byte

	err := c.retryPolicy.Execute(ctx, func() error {
		req, err := http.NewRequestWithContext(ctx, http.MethodPut, url, bytes.NewReader(body))
		if err != nil {
			return fmt.Errorf("failed to create request: %w", err)
		}

		for key, value := range headers {
			req.Header.Set(key, value)
		}

		if req.Header.Get("Content-Type") == "" {
			req.Header.Set("Content-Type", "application/json")
		}

		c.logger.Debug("HTTP PUT: %s", url)

		resp, err := c.httpClient.Do(req)
		if err != nil {
			return fmt.Errorf("request failed: %w", err)
		}
		defer resp.Body.Close()

		respBody, err := io.ReadAll(resp.Body)
		if err != nil {
			return fmt.Errorf("failed to read response body: %w", err)
		}

		if resp.StatusCode < 200 || resp.StatusCode >= 300 {
			return fmt.Errorf("HTTP %d: %s", resp.StatusCode, string(respBody))
		}

		responseBody = respBody
		return nil
	})

	if err != nil {
		return nil, err
	}

	return responseBody, nil
}

// Delete performs an HTTP DELETE request with retry logic
func (c *Client) Delete(ctx context.Context, url string, headers map[string]string) error {
	return c.retryPolicy.Execute(ctx, func() error {
		req, err := http.NewRequestWithContext(ctx, http.MethodDelete, url, nil)
		if err != nil {
			return fmt.Errorf("failed to create request: %w", err)
		}

		for key, value := range headers {
			req.Header.Set(key, value)
		}

		c.logger.Debug("HTTP DELETE: %s", url)

		resp, err := c.httpClient.Do(req)
		if err != nil {
			return fmt.Errorf("request failed: %w", err)
		}
		defer resp.Body.Close()

		if resp.StatusCode < 200 || resp.StatusCode >= 300 {
			body, _ := io.ReadAll(resp.Body)
			return fmt.Errorf("HTTP %d: %s", resp.StatusCode, string(body))
		}

		return nil
	})
}

// PostJSON is a convenience method for posting JSON data
func (c *Client) PostJSON(ctx context.Context, url string, payload interface{}, headers map[string]string) ([]byte, error) {
	jsonData, err := json.Marshal(payload)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal JSON: %w", err)
	}

	return c.Post(ctx, url, jsonData, headers)
}

// PutJSON is a convenience method for putting JSON data
func (c *Client) PutJSON(ctx context.Context, url string, payload interface{}, headers map[string]string) ([]byte, error) {
	jsonData, err := json.Marshal(payload)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal JSON: %w", err)
	}

	return c.Put(ctx, url, jsonData, headers)
}
