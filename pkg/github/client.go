package github

import (
	"context"
	"encoding/json"
	"fmt"
	"net/url"
	"strings"

	"github.com/github/gh-gei/pkg/http"
	"github.com/github/gh-gei/pkg/logger"
)

// Client is a GitHub API client
type Client struct {
	http   *http.Client
	apiURL string
	pat    string
	logger *logger.Logger
}

// Config contains configuration for the GitHub API client
type Config struct {
	APIURL      string // Default: "https://api.github.com"
	PAT         string // Personal Access Token (from GH_PAT, GH_SOURCE_PAT, or command line)
	NoSSLVerify bool   // For GHES with self-signed certificates
}

// DefaultConfig returns a Config with sensible defaults
func DefaultConfig() Config {
	return Config{
		APIURL:      "https://api.github.com",
		NoSSLVerify: false,
	}
}

// NewClient creates a new GitHub API client
func NewClient(cfg Config, httpClient *http.Client, log *logger.Logger) *Client {
	apiURL := cfg.APIURL
	if apiURL == "" {
		apiURL = "https://api.github.com"
	}

	// Trim trailing slash
	apiURL = strings.TrimRight(apiURL, "/")

	return &Client{
		http:   httpClient,
		apiURL: apiURL,
		pat:    cfg.PAT,
		logger: log,
	}
}

// GetRepos fetches all repositories for a given organization
// Corresponds to C# GithubApi.GetRepos() - line 114 in GithubApi.cs
func (c *Client) GetRepos(ctx context.Context, org string) ([]Repo, error) {
	// URL encode the org name
	escapedOrg := url.PathEscape(org)
	apiURL := fmt.Sprintf("%s/orgs/%s/repos?per_page=100", c.apiURL, escapedOrg)

	c.logger.Info("Fetching repositories for organization: %s", org)

	repos := []Repo{}
	page := 1

	for {
		pageURL := fmt.Sprintf("%s&page=%d", apiURL, page)

		headers := c.buildHeaders()
		body, err := c.http.Get(ctx, pageURL, headers)
		if err != nil {
			return nil, fmt.Errorf("failed to fetch repos (page %d): %w", page, err)
		}

		var pageRepos []map[string]interface{}
		if err := json.Unmarshal(body, &pageRepos); err != nil {
			return nil, fmt.Errorf("failed to parse repos response: %w", err)
		}

		// No more repos
		if len(pageRepos) == 0 {
			break
		}

		for _, repoData := range pageRepos {
			name, _ := repoData["name"].(string)
			visibility, _ := repoData["visibility"].(string)

			if name != "" {
				repos = append(repos, Repo{
					Name:       name,
					Visibility: visibility,
				})
			}
		}

		c.logger.Debug("Fetched %d repos from page %d", len(pageRepos), page)

		// Check if there are more pages
		// GitHub returns less than 100 if it's the last page
		if len(pageRepos) < 100 {
			break
		}

		page++
	}

	c.logger.Info("Found %d repositories in organization %s", len(repos), org)

	return repos, nil
}

// GetVersion fetches the GitHub Enterprise Server version
// Used by generate-script to determine if blob credentials are required
func (c *Client) GetVersion(ctx context.Context) (*VersionInfo, error) {
	// Only applicable for GHES
	if c.apiURL == "https://api.github.com" {
		return nil, fmt.Errorf("version endpoint not available on GitHub.com")
	}

	apiURL := fmt.Sprintf("%s/meta", c.apiURL)

	headers := c.buildHeaders()
	body, err := c.http.Get(ctx, apiURL, headers)
	if err != nil {
		return nil, fmt.Errorf("failed to fetch version: %w", err)
	}

	var meta map[string]interface{}
	if err := json.Unmarshal(body, &meta); err != nil {
		return nil, fmt.Errorf("failed to parse version response: %w", err)
	}

	version, _ := meta["installed_version"].(string)

	return &VersionInfo{
		Version:          version,
		InstalledVersion: version,
	}, nil
}

// buildHeaders constructs the HTTP headers for GitHub API requests
func (c *Client) buildHeaders() map[string]string {
	headers := map[string]string{
		"Accept":               "application/vnd.github+json",
		"X-GitHub-Api-Version": "2022-11-28",
	}

	if c.pat != "" {
		headers["Authorization"] = fmt.Sprintf("Bearer %s", c.pat)
	}

	return headers
}
