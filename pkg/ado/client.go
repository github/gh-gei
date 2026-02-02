package ado

import (
	"context"
	"encoding/json"
	"fmt"
	"net/url"
	"strings"

	"github.com/github/gh-gei/pkg/http"
	"github.com/github/gh-gei/pkg/logger"
)

// Client is a client for the Azure DevOps API
type Client struct {
	httpClient *http.Client
	baseURL    string
	log        *logger.Logger
	pat        string // Personal Access Token for authentication
}

// NewClient creates a new Azure DevOps API client
func NewClient(baseURL, pat string, log *logger.Logger, httpClient *http.Client) *Client {
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
		pat:        pat,
	}
}

// makeAuthHeaders creates authentication headers for ADO API requests
func (c *Client) makeAuthHeaders() map[string]string {
	return map[string]string{
		"Authorization": fmt.Sprintf("Basic %s", c.pat),
		"Content-Type":  "application/json",
	}
}

// GetTeamProjects retrieves all team projects in an organization
// Reference: AdoApi.cs line 157-162
func (c *Client) GetTeamProjects(ctx context.Context, org string) ([]TeamProject, error) {
	if org == "" {
		return nil, fmt.Errorf("org cannot be empty")
	}

	// URL encode the org name
	orgEscaped := url.PathEscape(org)
	apiURL := fmt.Sprintf("%s/%s/_apis/projects?api-version=6.1-preview", c.baseURL, orgEscaped)

	c.log.Debug("Fetching team projects for org: %s", org)

	body, err := c.httpClient.Get(ctx, apiURL, c.makeAuthHeaders())
	if err != nil {
		return nil, fmt.Errorf("failed to get team projects: %w", err)
	}

	var response teamProjectsResponse
	if err := json.Unmarshal([]byte(body), &response); err != nil {
		return nil, fmt.Errorf("failed to parse team projects response: %w", err)
	}

	c.log.Debug("Found %d team projects", len(response.Value))
	return response.Value, nil
}

// GetRepos retrieves all repositories in a team project
// Reference: AdoApi.cs line 166-179
func (c *Client) GetRepos(ctx context.Context, org, teamProject string) ([]Repository, error) {
	if org == "" {
		return nil, fmt.Errorf("org cannot be empty")
	}
	if teamProject == "" {
		return nil, fmt.Errorf("teamProject cannot be empty")
	}

	// URL encode the org and team project names
	orgEscaped := url.PathEscape(org)
	projectEscaped := url.PathEscape(teamProject)
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories?api-version=6.1-preview.1",
		c.baseURL, orgEscaped, projectEscaped)

	c.log.Debug("Fetching repos for org: %s, team project: %s", org, teamProject)

	body, err := c.httpClient.Get(ctx, apiURL, c.makeAuthHeaders())
	if err != nil {
		return nil, fmt.Errorf("failed to get repositories: %w", err)
	}

	var response repositoriesResponse
	if err := json.Unmarshal([]byte(body), &response); err != nil {
		return nil, fmt.Errorf("failed to parse repositories response: %w", err)
	}

	c.log.Debug("Found %d repositories", len(response.Value))
	return response.Value, nil
}

// GetEnabledRepos retrieves only enabled repositories in a team project
// Reference: AdoApi.cs line 164
func (c *Client) GetEnabledRepos(ctx context.Context, org, teamProject string) ([]Repository, error) {
	repos, err := c.GetRepos(ctx, org, teamProject)
	if err != nil {
		return nil, err
	}

	// Filter out disabled repos
	enabled := make([]Repository, 0, len(repos))
	for _, repo := range repos {
		if !repo.IsDisabled {
			enabled = append(enabled, repo)
		}
	}

	c.log.Debug("Found %d enabled repositories out of %d total", len(enabled), len(repos))
	return enabled, nil
}

// GetGithubAppId retrieves the GitHub App service connection ID for a GitHub organization
// by searching through team projects for a matching service endpoint
// Reference: AdoApi.cs line 181-212
func (c *Client) GetGithubAppId(ctx context.Context, org, githubOrg string, teamProjects []string) (string, error) {
	if org == "" {
		return "", fmt.Errorf("org cannot be empty")
	}
	if githubOrg == "" {
		return "", fmt.Errorf("githubOrg cannot be empty")
	}
	if len(teamProjects) == 0 {
		return "", nil
	}

	c.log.Debug("Searching for GitHub App ID for org: %s, GitHub org: %s", org, githubOrg)

	for _, teamProject := range teamProjects {
		appID, err := c.getTeamProjectGithubAppId(ctx, org, githubOrg, teamProject)
		if err != nil {
			c.log.Debug("Error checking team project %s: %v", teamProject, err)
			continue
		}
		if appID != "" {
			c.log.Debug("Found GitHub App ID: %s in team project: %s", appID, teamProject)
			return appID, nil
		}
	}

	c.log.Debug("No GitHub App ID found in any team project")
	return "", nil
}

// getTeamProjectGithubAppId retrieves the GitHub App ID for a specific team project
// Reference: AdoApi.cs line 200-212
func (c *Client) getTeamProjectGithubAppId(ctx context.Context, org, githubOrg, teamProject string) (string, error) {
	orgEscaped := url.PathEscape(org)
	projectEscaped := url.PathEscape(teamProject)
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4",
		c.baseURL, orgEscaped, projectEscaped)

	body, err := c.httpClient.Get(ctx, apiURL, c.makeAuthHeaders())
	if err != nil {
		return "", fmt.Errorf("failed to get service endpoints: %w", err)
	}

	var response serviceEndpointsResponse
	if err := json.Unmarshal([]byte(body), &response); err != nil {
		return "", fmt.Errorf("failed to parse service endpoints response: %w", err)
	}

	// Look for GitHub or GitHubProximaPipelines endpoint matching the GitHub org or team project
	for _, endpoint := range response.Value {
		// Check for GitHub type with matching org name
		if strings.EqualFold(endpoint.Type, "GitHub") && strings.EqualFold(endpoint.Name, githubOrg) {
			return endpoint.ID, nil
		}
		// Check for GitHubProximaPipelines type with matching team project name
		if strings.EqualFold(endpoint.Type, "GitHubProximaPipelines") && strings.EqualFold(endpoint.Name, teamProject) {
			return endpoint.ID, nil
		}
	}

	return "", nil
}
