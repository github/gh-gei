package bbs

import (
	"bytes"
	"context"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

const defaultPageSize = 100

// Client is a Bitbucket Server API client.
// It corresponds to the combination of C# BbsClient + BbsApi.
type Client struct {
	httpClient *http.Client
	baseURL    string
	authHeader string // "Basic base64(user:pass)" or empty
	log        *logger.Logger
}

// Option configures optional Client behavior.
type Option func(*Client)

// WithHTTPClient sets a custom *http.Client (useful for testing).
func WithHTTPClient(hc *http.Client) Option {
	return func(c *Client) { c.httpClient = hc }
}

// NewClient creates a Bitbucket Server API client.
// When username and password are both empty, no Authorization header is sent.
func NewClient(baseURL, username, password string, log *logger.Logger, opts ...Option) *Client {
	c := &Client{
		baseURL: strings.TrimRight(baseURL, "/"),
		log:     log,
	}
	if username != "" || password != "" {
		creds := base64.StdEncoding.EncodeToString([]byte(username + ":" + password))
		c.authHeader = "Basic " + creds
	}
	for _, o := range opts {
		o(c)
	}
	if c.httpClient == nil {
		c.httpClient = &http.Client{Timeout: 30 * time.Second}
	}
	return c
}

// ---------- low-level HTTP helpers ----------

// sendRequest builds, executes, and validates a single HTTP request.
// Returns body string, status code, and error.
func (c *Client) sendRequest(ctx context.Context, method, reqURL string, body interface{}) (string, int, error) {
	var bodyReader io.Reader
	if body != nil {
		data, err := json.Marshal(body)
		if err != nil {
			return "", 0, fmt.Errorf("marshal body: %w", err)
		}
		c.log.Verbose("HTTP BODY: %s", string(data))
		bodyReader = bytes.NewReader(data)
	}

	req, err := http.NewRequestWithContext(ctx, method, reqURL, bodyReader)
	if err != nil {
		return "", 0, fmt.Errorf("create request: %w", err)
	}
	if c.authHeader != "" {
		req.Header.Set("Authorization", c.authHeader)
	}
	req.Header.Set("Accept", "application/json")
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", 0, fmt.Errorf("request %s %s: %w", method, reqURL, err)
	}
	defer resp.Body.Close()

	respBody, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", resp.StatusCode, fmt.Errorf("read response: %w", err)
	}

	c.log.Verbose("RESPONSE (%d): %s", resp.StatusCode, string(respBody))

	if resp.StatusCode == http.StatusUnauthorized {
		return "", resp.StatusCode, cmdutil.NewUserError("Unauthorized. Please check your token and try again")
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return "", resp.StatusCode, fmt.Errorf("HTTP %d: %s", resp.StatusCode, string(respBody))
	}

	return string(respBody), resp.StatusCode, nil
}

// get performs a GET request.
func (c *Client) get(ctx context.Context, reqURL string) (string, int, error) {
	c.log.Verbose("HTTP GET: %s", reqURL)
	return c.sendRequest(ctx, http.MethodGet, reqURL, nil)
}

// post performs a POST request with a JSON body.
func (c *Client) post(ctx context.Context, reqURL string, payload interface{}) (string, error) {
	c.log.Verbose("HTTP POST: %s", reqURL)
	body, _, err := c.sendRequest(ctx, http.MethodPost, reqURL, payload)
	return body, err
}

// getAll performs paginated GET requests and collects all values.
// BBS pagination uses isLastPage + nextPageStart + values[].
func getAll[T any](ctx context.Context, c *Client, rawURL string) ([]T, error) {
	var all []T
	start := 0
	for {
		pageURL := addPaginationParams(rawURL, start, defaultPageSize)
		body, _, err := c.get(ctx, pageURL)
		if err != nil {
			return nil, err
		}
		var page paginatedResponse[T]
		if err := json.Unmarshal([]byte(body), &page); err != nil {
			return nil, fmt.Errorf("parse paginated response: %w", err)
		}
		all = append(all, page.Values...)
		if page.IsLastPage {
			break
		}
		start = page.NextPageStart
	}
	return all, nil
}

// addPaginationParams adds or replaces start/limit query parameters.
func addPaginationParams(rawURL string, start, limit int) string {
	u, err := url.Parse(rawURL)
	if err != nil {
		// Fallback: just append
		return fmt.Sprintf("%s?start=%d&limit=%d", rawURL, start, limit)
	}
	q := u.Query()
	q.Set("start", fmt.Sprintf("%d", start))
	q.Set("limit", fmt.Sprintf("%d", limit))
	u.RawQuery = q.Encode()
	return u.String()
}

// ---------- public API methods ----------

// GetServerVersion returns the Bitbucket Server version string.
func (c *Client) GetServerVersion(ctx context.Context) (string, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/application-properties", c.baseURL)
	body, _, err := c.get(ctx, reqURL)
	if err != nil {
		return "", err
	}
	var result struct {
		Version string `json:"version"`
	}
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		return "", fmt.Errorf("parse server version: %w", err)
	}
	return result.Version, nil
}

// StartExport starts a repository export and returns the export ID.
func (c *Client) StartExport(ctx context.Context, projectKey, slug string) (int64, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/migration/exports", c.baseURL)
	payload := map[string]interface{}{
		"repositoriesRequest": map[string]interface{}{
			"includes": []map[string]string{
				{"projectKey": projectKey, "slug": slug},
			},
		},
	}
	body, err := c.post(ctx, reqURL, payload)
	if err != nil {
		return 0, err
	}
	var result struct {
		ID int64 `json:"id"`
	}
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		return 0, fmt.Errorf("parse export response: %w", err)
	}
	return result.ID, nil
}

// GetExport returns the state, message, and percentage of an export.
func (c *Client) GetExport(ctx context.Context, id int64) (state string, message string, percentage int, err error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/migration/exports/%d", c.baseURL, id)
	body, _, getErr := c.get(ctx, reqURL)
	if getErr != nil {
		err = getErr
		return
	}
	var result exportState
	if err = json.Unmarshal([]byte(body), &result); err != nil {
		err = fmt.Errorf("parse export state: %w", err)
		return
	}
	return result.State, result.Progress.Message, result.Progress.Percentage, nil
}

// GetProjects returns all projects in the Bitbucket Server instance.
func (c *Client) GetProjects(ctx context.Context) ([]Project, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/projects", c.baseURL)
	return getAll[Project](ctx, c, reqURL)
}

// GetProject returns a single project by key.
func (c *Client) GetProject(ctx context.Context, projectKey string) (Project, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/projects/%s", c.baseURL, url.PathEscape(projectKey))
	body, _, err := c.get(ctx, reqURL)
	if err != nil {
		return Project{}, err
	}
	var p Project
	if err := json.Unmarshal([]byte(body), &p); err != nil {
		return Project{}, fmt.Errorf("parse project: %w", err)
	}
	return p, nil
}

// GetRepos returns all repositories in a project.
func (c *Client) GetRepos(ctx context.Context, projectKey string) ([]Repository, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/projects/%s/repos", c.baseURL, url.PathEscape(projectKey))
	return getAll[Repository](ctx, c, reqURL)
}

// GetIsRepositoryArchived returns whether a repository is archived.
func (c *Client) GetIsRepositoryArchived(ctx context.Context, projectKey, repo string) (bool, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/projects/%s/repos/%s?fields=archived",
		c.baseURL, url.PathEscape(projectKey), url.PathEscape(repo))
	body, _, err := c.get(ctx, reqURL)
	if err != nil {
		return false, err
	}
	var result struct {
		Archived bool `json:"archived"`
	}
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		return false, fmt.Errorf("parse archived status: %w", err)
	}
	return result.Archived, nil
}

// GetRepositoryPullRequests returns all pull requests for a repository.
func (c *Client) GetRepositoryPullRequests(ctx context.Context, projectKey, repo string) ([]PullRequest, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/projects/%s/repos/%s/pull-requests?state=all",
		c.baseURL, url.PathEscape(projectKey), url.PathEscape(repo))
	return getAll[PullRequest](ctx, c, reqURL)
}

// GetRepositoryLatestCommitDate returns the timestamp of the most recent commit.
// Returns nil if the repository has no commits or if the repo returns 404.
func (c *Client) GetRepositoryLatestCommitDate(ctx context.Context, projectKey, repo string) (*time.Time, error) {
	reqURL := fmt.Sprintf("%s/rest/api/1.0/projects/%s/repos/%s/commits?limit=1",
		c.baseURL, url.PathEscape(projectKey), url.PathEscape(repo))
	body, statusCode, err := c.get(ctx, reqURL)
	if err != nil {
		if statusCode == http.StatusNotFound {
			return nil, nil
		}
		return nil, err
	}

	var result struct {
		Values []struct {
			AuthorTimestamp int64 `json:"authorTimestamp"`
		} `json:"values"`
	}
	if err := json.Unmarshal([]byte(body), &result); err != nil {
		return nil, fmt.Errorf("parse commits: %w", err)
	}
	if len(result.Values) == 0 {
		return nil, nil
	}

	ts := time.UnixMilli(result.Values[0].AuthorTimestamp).UTC()
	return &ts, nil
}

// GetRepositoryAndAttachmentsSize returns the repository and attachments sizes in bytes.
// Note: this endpoint does NOT use the /rest/api/1.0/ prefix (matching C# behavior).
func (c *Client) GetRepositoryAndAttachmentsSize(ctx context.Context, projectKey, repo string) (repoSize, attachmentsSize uint64, err error) {
	reqURL := fmt.Sprintf("%s/projects/%s/repos/%s/sizes",
		c.baseURL, url.PathEscape(projectKey), url.PathEscape(repo))
	body, _, getErr := c.get(ctx, reqURL)
	if getErr != nil {
		err = getErr
		return
	}
	var result struct {
		Repository  uint64 `json:"repository"`
		Attachments uint64 `json:"attachments"`
	}
	if err = json.Unmarshal([]byte(body), &result); err != nil {
		err = fmt.Errorf("parse sizes: %w", err)
		return
	}
	return result.Repository, result.Attachments, nil
}
