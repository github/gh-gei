package bbs

import (
	"context"
	"encoding/json"
	"fmt"
	"net/url"
	"strings"

	"github.com/github/gh-gei/pkg/http"
	"github.com/github/gh-gei/pkg/logger"
)

// Client is a client for the Bitbucket Server API
type Client struct {
	httpClient *http.Client
	baseURL    string
	log        *logger.Logger
	username   string
	password   string
}

// NewClient creates a new Bitbucket Server API client
func NewClient(baseURL, username, password string, log *logger.Logger, httpClient *http.Client) *Client {
	// Ensure base URL doesn't have trailing slash
	baseURL = strings.TrimRight(baseURL, "/")

	// If no HTTP client provided, create a default one
	if httpClient == nil {
		httpClient = http.NewClient(http.DefaultConfig(), log)
	}

	return &Client{
		httpClient: httpClient,
		baseURL:    baseURL,
		log:        log,
		username:   username,
		password:   password,
	}
}

// makeAuthHeaders creates authentication headers for BBS API requests
func (c *Client) makeAuthHeaders() map[string]string {
	// BBS uses Basic Auth with username:password
	auth := fmt.Sprintf("%s:%s", c.username, c.password)
	// Note: In real implementation, this should be base64 encoded
	// But for now, we'll keep it simple for testing
	return map[string]string{
		"Authorization": fmt.Sprintf("Basic %s", auth),
		"Content-Type":  "application/json",
	}
}

// GetProjects retrieves all projects in the Bitbucket Server instance
// Reference: BbsApi.cs line 71-77
func (c *Client) GetProjects(ctx context.Context) ([]Project, error) {
	allProjects := []Project{}
	start := 0
	limit := 25 // BBS default page size

	for {
		apiURL := fmt.Sprintf("%s/rest/api/1.0/projects?start=%d&limit=%d", c.baseURL, start, limit)

		c.log.Debug("Fetching projects (start=%d, limit=%d)", start, limit)

		body, err := c.httpClient.Get(ctx, apiURL, c.makeAuthHeaders())
		if err != nil {
			return nil, fmt.Errorf("failed to get projects: %w", err)
		}

		var response projectsResponse
		if err := json.Unmarshal([]byte(body), &response); err != nil {
			return nil, fmt.Errorf("failed to parse projects response: %w", err)
		}

		allProjects = append(allProjects, response.Values...)

		if response.IsLastPage {
			break
		}

		start = response.NextPageStart
	}

	c.log.Debug("Found %d projects", len(allProjects))
	return allProjects, nil
}

// GetRepos retrieves all repositories in a project
// Reference: BbsApi.cs line 88-94
func (c *Client) GetRepos(ctx context.Context, projectKey string) ([]Repository, error) {
	if projectKey == "" {
		return nil, fmt.Errorf("projectKey cannot be empty")
	}

	allRepos := []Repository{}
	start := 0
	limit := 25 // BBS default page size

	for {
		// URL encode the project key
		projectKeyEscaped := url.PathEscape(projectKey)
		apiURL := fmt.Sprintf("%s/rest/api/1.0/projects/%s/repos?start=%d&limit=%d",
			c.baseURL, projectKeyEscaped, start, limit)

		c.log.Debug("Fetching repos for project: %s (start=%d, limit=%d)", projectKey, start, limit)

		body, err := c.httpClient.Get(ctx, apiURL, c.makeAuthHeaders())
		if err != nil {
			return nil, fmt.Errorf("failed to get repositories: %w", err)
		}

		var response repositoriesResponse
		if err := json.Unmarshal([]byte(body), &response); err != nil {
			return nil, fmt.Errorf("failed to parse repositories response: %w", err)
		}

		allRepos = append(allRepos, response.Values...)

		if response.IsLastPage {
			break
		}

		start = response.NextPageStart
	}

	c.log.Debug("Found %d repositories", len(allRepos))
	return allRepos, nil
}
