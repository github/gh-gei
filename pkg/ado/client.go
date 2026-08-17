package ado

import (
	"bytes"
	"context"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

const nullStr = "null"

// Client is a complete Azure DevOps API client.
// It corresponds to the combination of C# AdoClient + AdoApi.
type Client struct {
	httpClient *http.Client
	baseURL    string
	pat        string // base64-encoded ":PAT"
	log        *logger.Logger

	retryDelay time.Duration // cooperative Retry-After throttle

	// caches (matching C# behavior)
	repoIDs     map[repoIDKey]map[string]string // (org,project) → (repoName → id)
	pipelineIDs map[pipelineIDKey]int           // (org,project,path) → id
}

// Option configures optional Client behavior.
type Option func(*Client)

// WithHTTPClient sets a custom *http.Client (useful for testing).
func WithHTTPClient(hc *http.Client) Option {
	return func(c *Client) { c.httpClient = hc }
}

// NewClient creates an ADO API client.
// pat is the raw Personal Access Token; it is base64-encoded internally.
func NewClient(baseURL, pat string, log *logger.Logger, opts ...Option) *Client {
	c := &Client{
		baseURL:     strings.TrimRight(baseURL, "/"),
		pat:         base64.StdEncoding.EncodeToString([]byte(":" + pat)),
		log:         log,
		repoIDs:     make(map[repoIDKey]map[string]string),
		pipelineIDs: make(map[pipelineIDKey]int),
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

// applyRetryDelay sleeps if a Retry-After was recorded from a prior response.
func (c *Client) applyRetryDelay(ctx context.Context) error {
	if c.retryDelay > 0 {
		c.log.Warning("THROTTLING IN EFFECT. Waiting %d ms", c.retryDelay.Milliseconds())
		select {
		case <-time.After(c.retryDelay):
		case <-ctx.Done():
			return ctx.Err()
		}
		c.retryDelay = 0
	}
	return nil
}

// checkForRetryDelay reads the Retry-After delta from a response.
func (c *Client) checkForRetryDelay(resp *http.Response) {
	ra := resp.Header.Get("Retry-After")
	if ra == "" {
		return
	}
	sec, err := strconv.Atoi(ra)
	if err == nil && sec > 0 {
		c.retryDelay = time.Duration(sec) * time.Second
	}
}

// sendRequest builds, executes and validates a single HTTP request.
// Returns body string, response headers, and error.
func (c *Client) sendRequest(ctx context.Context, method, reqURL string, body interface{}) (string, http.Header, error) {
	var bodyReader io.Reader
	if body != nil {
		data, err := json.Marshal(body)
		if err != nil {
			return "", nil, fmt.Errorf("marshal body: %w", err)
		}
		c.log.Verbose("HTTP BODY: %s", string(data))
		bodyReader = bytes.NewReader(data)
	}

	req, err := http.NewRequestWithContext(ctx, method, reqURL, bodyReader)
	if err != nil {
		return "", nil, fmt.Errorf("create request: %w", err)
	}
	req.Header.Set("Authorization", "Basic "+c.pat)
	req.Header.Set("Accept", "application/json")
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", nil, fmt.Errorf("request %s %s: %w", method, reqURL, err)
	}
	defer resp.Body.Close()

	respBody, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", nil, fmt.Errorf("read response: %w", err)
	}

	c.log.Verbose("RESPONSE (%d): %s", resp.StatusCode, string(respBody))

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return "", resp.Header, fmt.Errorf("HTTP %d: %s", resp.StatusCode, string(respBody))
	}

	c.checkForRetryDelay(resp)
	return string(respBody), resp.Header, nil
}

// get performs a GET with retry (3 attempts, exponential backoff).
// Returns body string, response headers, error.
func (c *Client) get(ctx context.Context, reqURL string) (string, http.Header, error) {
	if err := c.applyRetryDelay(ctx); err != nil {
		return "", nil, err
	}
	c.log.Verbose("HTTP GET: %s", reqURL)

	var lastErr error
	for attempt := 0; attempt < 3; attempt++ {
		if attempt > 0 {
			delay := time.Duration(1<<uint(attempt-1)) * time.Second
			select {
			case <-time.After(delay):
			case <-ctx.Done():
				return "", nil, ctx.Err()
			}
		}
		body, headers, err := c.sendRequest(ctx, http.MethodGet, reqURL, nil)
		if err == nil {
			return body, headers, nil
		}
		lastErr = err
	}
	return "", nil, lastErr
}

// post performs a POST (no retry, matching C#).
func (c *Client) post(ctx context.Context, reqURL string, payload interface{}) (string, error) {
	if err := c.applyRetryDelay(ctx); err != nil {
		return "", err
	}
	c.log.Verbose("HTTP POST: %s", reqURL)
	body, _, err := c.sendRequest(ctx, http.MethodPost, reqURL, payload)
	return body, err
}

// put performs a PUT (no retry).
func (c *Client) put(ctx context.Context, reqURL string, payload interface{}) (string, error) {
	if err := c.applyRetryDelay(ctx); err != nil {
		return "", err
	}
	c.log.Verbose("HTTP PUT: %s", reqURL)
	body, _, err := c.sendRequest(ctx, http.MethodPut, reqURL, payload)
	return body, err
}

// patch performs a PATCH (no retry).
func (c *Client) patch(ctx context.Context, reqURL string, payload interface{}) (string, error) {
	if err := c.applyRetryDelay(ctx); err != nil {
		return "", err
	}
	c.log.Verbose("HTTP PATCH: %s", reqURL)
	body, _, err := c.sendRequest(ctx, http.MethodPatch, reqURL, payload)
	return body, err
}

// GetRaw performs a raw GET request to the given URL and returns the response body.
// This is used by services that need to make API calls with fully-formed URLs.
func (c *Client) GetRaw(ctx context.Context, url string) (string, error) {
	body, _, err := c.get(ctx, url)
	return body, err
}

// PutRaw performs a raw PUT request to the given URL with the given body.
// This is used by services that need to make API calls with fully-formed URLs.
func (c *Client) PutRaw(ctx context.Context, url string, payload interface{}) (string, error) {
	return c.put(ctx, url, payload)
}

// deleteReq performs a DELETE (no retry).
func (c *Client) deleteReq(ctx context.Context, reqURL string) (string, error) {
	if err := c.applyRetryDelay(ctx); err != nil {
		return "", err
	}
	c.log.Verbose("HTTP DELETE: %s", reqURL)
	body, _, err := c.sendRequest(ctx, http.MethodDelete, reqURL, nil)
	return body, err
}

// ---------- pagination helpers ----------

// getWithPaging fetches all pages using continuation-token pagination.
// Retries on 503 specifically (matching C# GetWithPagingAsync).
func (c *Client) getWithPaging(ctx context.Context, reqURL string) ([]json.RawMessage, error) {
	return c.getWithPagingToken(ctx, reqURL, "")
}

func (c *Client) getWithPagingToken(ctx context.Context, reqURL, continuationToken string) ([]json.RawMessage, error) {
	u := reqURL
	if continuationToken != "" {
		if strings.Contains(u, "?") {
			u += "&"
		} else {
			u += "?"
		}
		u += "continuationToken=" + continuationToken
	}

	if err := c.applyRetryDelay(ctx); err != nil {
		return nil, err
	}
	c.log.Verbose("HTTP GET: %s", u)

	// Retry loop with special 503 handling
	var body string
	var headers http.Header
	var lastErr error
	for attempt := 0; attempt < 3; attempt++ {
		if attempt > 0 {
			delay := time.Duration(1<<uint(attempt-1)) * time.Second
			select {
			case <-time.After(delay):
			case <-ctx.Done():
				return nil, ctx.Err()
			}
		}
		var err error
		body, headers, err = c.sendRequest(ctx, http.MethodGet, u, nil)
		if err == nil {
			lastErr = nil
			break
		}
		// Retry on 503, fail fast on others
		if !strings.Contains(err.Error(), "HTTP 503") {
			return nil, err
		}
		lastErr = err
	}
	if lastErr != nil {
		return nil, lastErr
	}

	// Parse {"value": [...]}
	var envelope struct {
		Value []json.RawMessage `json:"value"`
	}
	if err := json.Unmarshal([]byte(body), &envelope); err != nil {
		return nil, fmt.Errorf("parse paging response: %w", err)
	}

	result := envelope.Value

	// Check for continuation
	if tok := headers.Get("x-ms-continuationtoken"); tok != "" {
		more, err := c.getWithPagingToken(ctx, reqURL, tok)
		if err != nil {
			return nil, err
		}
		result = append(result, more...)
	}

	return result, nil
}

// getWithPagingTopSkip uses $top/$skip pagination to fetch all items,
// applying selector to each raw JSON item.
func getWithPagingTopSkip[T any](c *Client, ctx context.Context, reqURL string, selector func(json.RawMessage) (T, error)) ([]T, error) {
	return getWithPagingTopSkipAt(c, ctx, reqURL, 0, selector)
}

func getWithPagingTopSkipAt[T any](c *Client, ctx context.Context, reqURL string, skip int, selector func(json.RawMessage) (T, error)) ([]T, error) {
	const pageSize = 1000

	u := reqURL
	if strings.Contains(u, "?") {
		u += "&"
	} else {
		u += "?"
	}
	u += fmt.Sprintf("$skip=%d&$top=%d", skip, pageSize)

	body, _, err := c.get(ctx, u)
	if err != nil {
		return nil, err
	}

	var envelope struct {
		Value []json.RawMessage `json:"value"`
	}
	if err := json.Unmarshal([]byte(body), &envelope); err != nil {
		return nil, fmt.Errorf("parse top/skip response: %w", err)
	}

	var result []T
	for _, raw := range envelope.Value {
		item, err := selector(raw)
		if err != nil {
			return nil, err
		}
		result = append(result, item)
	}

	if len(envelope.Value) > 0 {
		more, err := getWithPagingTopSkipAt(c, ctx, reqURL, skip+pageSize, selector)
		if err != nil {
			return nil, err
		}
		result = append(result, more...)
	}

	return result, nil
}

// getCountUsingSkip uses binary search to count items at a URL using $skip/$top.
func (c *Client) getCountUsingSkip(ctx context.Context, reqURL string) (int, error) {
	exists, err := c.doesSkipExist(ctx, reqURL, 0)
	if err != nil {
		return 0, err
	}
	if !exists {
		return 0, nil
	}

	minCount := 1
	maxCount := 500

	for {
		exists, err := c.doesSkipExist(ctx, reqURL, maxCount)
		if err != nil {
			return 0, err
		}
		if !exists {
			break
		}
		maxCount *= 2
	}

	skip := 500
	for minCount < maxCount {
		exists, err := c.doesSkipExist(ctx, reqURL, skip)
		if err != nil {
			return 0, err
		}
		if exists {
			minCount = skip + 1
		} else {
			maxCount = skip
		}
		skip = ((maxCount - minCount) / 2) + minCount
	}

	return minCount, nil
}

func (c *Client) doesSkipExist(ctx context.Context, reqURL string, skip int) (bool, error) {
	u := reqURL
	if strings.Contains(u, "?") {
		u += "&"
	} else {
		u += "?"
	}
	u += fmt.Sprintf("$top=1&$skip=%d", skip)

	body, _, err := c.get(ctx, u)
	if err != nil {
		return false, err
	}

	var envelope struct {
		Count int `json:"count"`
	}
	if err := json.Unmarshal([]byte(body), &envelope); err != nil {
		return false, fmt.Errorf("parse count response: %w", err)
	}
	return envelope.Count > 0, nil
}

// extractErrorMessage checks a HierarchyQuery response for errorMessage.
func extractErrorMessage(response, dataProviderKey string) string {
	if response == "" {
		return ""
	}
	var data map[string]json.RawMessage
	if err := json.Unmarshal([]byte(response), &data); err != nil {
		return ""
	}
	dpRaw, ok := data["dataProviders"]
	if !ok {
		return ""
	}
	var dataProviders map[string]json.RawMessage
	if err := json.Unmarshal(dpRaw, &dataProviders); err != nil {
		return ""
	}
	provRaw, ok := dataProviders[dataProviderKey]
	if !ok {
		return ""
	}
	var provider struct {
		ErrorMessage string `json:"errorMessage"`
	}
	if err := json.Unmarshal(provRaw, &provider); err != nil {
		return ""
	}
	return provider.ErrorMessage
}

// ---------- ADO API methods ----------

// GetOrgOwner returns the org owner as "name (email)".
func (c *Client) GetOrgOwner(ctx context.Context, org string) (string, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org))

	payload := map[string]interface{}{
		"contributionIds": []string{"ms.vss-admin-web.organization-admin-overview-delay-load-data-provider"},
		"dataProviderContext": map[string]interface{}{
			"properties": map[string]interface{}{
				"sourcePage": map[string]interface{}{
					"routeValues": map[string]interface{}{
						"adminPivot": "organizationOverview",
					},
				},
			},
		},
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return "", fmt.Errorf("get org owner: %w", err)
	}

	var data struct {
		DataProviders map[string]struct {
			CurrentOwner struct {
				Name  string `json:"name"`
				Email string `json:"email"`
			} `json:"currentOwner"`
		} `json:"dataProviders"`
	}
	if err := json.Unmarshal([]byte(resp), &data); err != nil {
		return "", fmt.Errorf("parse org owner response: %w", err)
	}

	dp, ok := data.DataProviders["ms.vss-admin-web.organization-admin-overview-delay-load-data-provider"]
	if !ok {
		return "", fmt.Errorf("missing data provider in org owner response")
	}
	return fmt.Sprintf("%s (%s)", dp.CurrentOwner.Name, dp.CurrentOwner.Email), nil
}

// GetUserId returns the PublicAlias of the authenticated user.
func (c *Client) GetUserId(ctx context.Context) (string, error) {
	apiURL := "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1"
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return "", fmt.Errorf("get user id: %w", err)
	}

	var data struct {
		CoreAttributes struct {
			PublicAlias struct {
				Value string `json:"value"`
			} `json:"PublicAlias"`
		} `json:"coreAttributes"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return "", fmt.Errorf("parse user id response: %w", err)
	}

	uid := data.CoreAttributes.PublicAlias.Value
	if uid == "" {
		return "", fmt.Errorf("unexpected response when retrieving User ID")
	}
	return uid, nil
}

// GetOrganizations returns organization names for a user.
func (c *Client) GetOrganizations(ctx context.Context, userId string) ([]string, error) {
	apiURL := fmt.Sprintf("https://app.vssps.visualstudio.com/_apis/accounts?memberId=%s?api-version=5.0-preview.1",
		url.PathEscape(userId))
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return nil, fmt.Errorf("get organizations: %w", err)
	}

	var items []struct {
		AccountName string `json:"AccountName"`
	}
	if err := json.Unmarshal([]byte(body), &items); err != nil {
		return nil, fmt.Errorf("parse organizations response: %w", err)
	}

	names := make([]string, 0, len(items))
	for _, item := range items {
		names = append(names, item.AccountName)
	}
	return names, nil
}

// GetOrganizationId returns the accountId for a specific ADO organization.
func (c *Client) GetOrganizationId(ctx context.Context, userId, adoOrg string) (string, error) {
	apiURL := fmt.Sprintf("https://app.vssps.visualstudio.com/_apis/accounts?memberId=%s&api-version=5.0-preview.1",
		url.PathEscape(userId))
	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return "", fmt.Errorf("get organization id: %w", err)
	}

	for _, raw := range items {
		var acct struct {
			AccountName string `json:"accountName"`
			AccountID   string `json:"accountId"`
		}
		if err := json.Unmarshal(raw, &acct); err != nil {
			continue
		}
		if strings.EqualFold(acct.AccountName, adoOrg) {
			return acct.AccountID, nil
		}
	}
	return "", fmt.Errorf("organization %q not found", adoOrg)
}

// GetTeamProjects returns project names in an org (using continuation-token paging).
func (c *Client) GetTeamProjects(ctx context.Context, org string) ([]string, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/projects?api-version=6.1-preview",
		c.baseURL, url.PathEscape(org))
	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return nil, fmt.Errorf("get team projects: %w", err)
	}

	names := make([]string, 0, len(items))
	for _, raw := range items {
		var proj struct {
			Name string `json:"name"`
		}
		if err := json.Unmarshal(raw, &proj); err != nil {
			return nil, fmt.Errorf("parse project: %w", err)
		}
		names = append(names, proj.Name)
	}
	return names, nil
}

// GetTeamProjectId returns the id of a specific team project.
func (c *Client) GetTeamProjectId(ctx context.Context, org, teamProject string) (string, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/projects/%s?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject))
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return "", fmt.Errorf("get team project id: %w", err)
	}

	var proj struct {
		ID string `json:"id"`
	}
	if err := json.Unmarshal([]byte(body), &proj); err != nil {
		return "", fmt.Errorf("parse team project id: %w", err)
	}
	return proj.ID, nil
}

// GetRepos returns all repositories in a team project.
func (c *Client) GetRepos(ctx context.Context, org, teamProject string) ([]Repository, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories?api-version=6.1-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject))
	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return nil, fmt.Errorf("get repos: %w", err)
	}

	repos := make([]Repository, 0, len(items))
	for _, raw := range items {
		var r Repository
		if err := json.Unmarshal(raw, &r); err != nil {
			return nil, fmt.Errorf("parse repo: %w", err)
		}
		repos = append(repos, r)
	}
	return repos, nil
}

// GetEnabledRepos returns only non-disabled repositories.
func (c *Client) GetEnabledRepos(ctx context.Context, org, teamProject string) ([]Repository, error) {
	repos, err := c.GetRepos(ctx, org, teamProject)
	if err != nil {
		return nil, err
	}
	enabled := make([]Repository, 0, len(repos))
	for _, r := range repos {
		if !r.IsDisabled {
			enabled = append(enabled, r)
		}
	}
	return enabled, nil
}

// GetRepoId returns the id of a specific repo, falling back to cache on 404.
func (c *Client) GetRepoId(ctx context.Context, org, teamProject, repo string) (string, error) {
	key := repoIDKey{strings.ToUpper(org), strings.ToUpper(teamProject)}
	if cache, ok := c.repoIDs[key]; ok {
		if id, ok := cache[strings.ToUpper(repo)]; ok {
			return id, nil
		}
	}

	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s?api-version=4.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repo))
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		// On 404, fall back to cache
		if strings.Contains(err.Error(), "HTTP 404") {
			if err2 := c.PopulateRepoIdCache(ctx, org, teamProject); err2 != nil {
				return "", err2
			}
			if cache, ok := c.repoIDs[key]; ok {
				if id, ok := cache[strings.ToUpper(repo)]; ok {
					return id, nil
				}
			}
			return "", fmt.Errorf("repo %q not found in %s/%s", repo, org, teamProject)
		}
		return "", fmt.Errorf("get repo id: %w", err)
	}

	var r struct {
		ID string `json:"id"`
	}
	if err := json.Unmarshal([]byte(body), &r); err != nil {
		return "", fmt.Errorf("parse repo id: %w", err)
	}
	return r.ID, nil
}

// PopulateRepoIdCache fetches all repos and populates the in-memory cache.
func (c *Client) PopulateRepoIdCache(ctx context.Context, org, teamProject string) error {
	key := repoIDKey{strings.ToUpper(org), strings.ToUpper(teamProject)}
	if _, ok := c.repoIDs[key]; ok {
		return nil
	}

	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories?api-version=4.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject))
	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return fmt.Errorf("populate repo id cache: %w", err)
	}

	ids := make(map[string]string)
	for _, raw := range items {
		var r struct {
			ID   string `json:"id"`
			Name string `json:"name"`
		}
		if err := json.Unmarshal(raw, &r); err != nil {
			continue
		}
		nameUpper := strings.ToUpper(r.Name)
		if _, exists := ids[nameUpper]; exists {
			c.log.Warning("Multiple repos with the same name were found [org: %s project: %s repo: %s]. Ignoring repo ID %s", org, teamProject, r.Name, r.ID)
			continue
		}
		ids[nameUpper] = r.ID
	}
	c.repoIDs[key] = ids
	return nil
}

// GetLastPushDate returns the date of the most recent push to a repo.
func (c *Client) GetLastPushDate(ctx context.Context, org, teamProject, repo string) (time.Time, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s/pushes?$top=1&api-version=7.1-preview.2",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repo))
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return time.Time{}, fmt.Errorf("get last push date: %w", err)
	}

	var data struct {
		Value []struct {
			Date time.Time `json:"date"`
		} `json:"value"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return time.Time{}, fmt.Errorf("parse last push date: %w", err)
	}

	if len(data.Value) == 0 {
		return time.Time{}, nil
	}

	d := data.Value[0].Date
	// Truncate to date only (matching C# .Date)
	return time.Date(d.Year(), d.Month(), d.Day(), 0, 0, 0, 0, d.Location()), nil
}

// GetCommitCountSince returns the number of commits since fromDate.
func (c *Client) GetCommitCountSince(ctx context.Context, org, teamProject, repo string, fromDate time.Time) (int, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s/commits?searchCriteria.fromDate=%s&api-version=7.1-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repo),
		fromDate.Format("01/02/2006"))
	return c.getCountUsingSkip(ctx, apiURL)
}

// GetPushersSince returns distinct "displayName (uniqueName)" strings of pushers.
func (c *Client) GetPushersSince(ctx context.Context, org, teamProject, repo string, fromDate time.Time) ([]string, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s/pushes?searchCriteria.fromDate=%s&api-version=7.1-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repo),
		fromDate.Format("01/02/2006"))
	return getWithPagingTopSkip(c, ctx, apiURL, func(raw json.RawMessage) (string, error) {
		var item struct {
			PushedBy struct {
				DisplayName string `json:"displayName"`
				UniqueName  string `json:"uniqueName"`
			} `json:"pushedBy"`
		}
		if err := json.Unmarshal(raw, &item); err != nil {
			return "", err
		}
		return fmt.Sprintf("%s (%s)", item.PushedBy.DisplayName, item.PushedBy.UniqueName), nil
	})
}

// GetPullRequestCount returns the total number of pull requests for a repo.
func (c *Client) GetPullRequestCount(ctx context.Context, org, teamProject, repo string) (int, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s/pullrequests?searchCriteria.status=all&api-version=7.1-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repo))
	return c.getCountUsingSkip(ctx, apiURL)
}

// GetGithubAppId searches team projects for a GitHub service connection.
func (c *Client) GetGithubAppId(ctx context.Context, org, githubOrg string, teamProjects []string) (string, error) {
	if len(teamProjects) == 0 {
		return "", nil
	}
	for _, tp := range teamProjects {
		id, err := c.getTeamProjectGithubAppId(ctx, org, githubOrg, tp)
		if err != nil {
			c.log.Debug("Error checking team project %s: %v", tp, err)
			continue
		}
		if id != "" {
			return id, nil
		}
	}
	return "", nil
}

func (c *Client) getTeamProjectGithubAppId(ctx context.Context, org, githubOrg, teamProject string) (string, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject))
	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return "", err
	}

	for _, raw := range items {
		var ep struct {
			ID   string `json:"id"`
			Type string `json:"type"`
			Name string `json:"name"`
		}
		if err := json.Unmarshal(raw, &ep); err != nil {
			continue
		}
		if strings.EqualFold(ep.Type, "GitHub") && strings.EqualFold(ep.Name, githubOrg) {
			return ep.ID, nil
		}
		if strings.EqualFold(ep.Type, "GitHubProximaPipelines") && strings.EqualFold(ep.Name, teamProject) {
			return ep.ID, nil
		}
	}
	return "", nil
}

// ContainsServiceConnection checks if a service connection exists and is shared with a project.
func (c *Client) ContainsServiceConnection(ctx context.Context, org, teamProject, serviceConnectionId string) (bool, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/serviceendpoint/endpoints/%s?api-version=6.0-preview.4",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(serviceConnectionId))
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return false, fmt.Errorf("check service connection: %w", err)
	}
	return body != "" && !strings.EqualFold(strings.TrimSpace(body), nullStr), nil
}

// ShareServiceConnection shares a service connection with a team project.
func (c *Client) ShareServiceConnection(ctx context.Context, org, teamProject, teamProjectId, serviceConnectionId string) error {
	apiURL := fmt.Sprintf("%s/%s/_apis/serviceendpoint/endpoints/%s?api-version=6.0-preview.4",
		c.baseURL, url.PathEscape(org), url.PathEscape(serviceConnectionId))

	payload := []map[string]interface{}{
		{
			"name": fmt.Sprintf("%s-%s", org, teamProject),
			"projectReference": map[string]interface{}{
				"id":   teamProjectId,
				"name": teamProject,
			},
		},
	}

	_, err := c.patch(ctx, apiURL, payload)
	return err
}

// GetGithubHandle retrieves the GitHub login for the user via a HierarchyQuery.
func (c *Client) GetGithubHandle(ctx context.Context, org, teamProject, githubToken string) (string, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org))

	payload := map[string]interface{}{
		"contributionIds": []string{"ms.vss-work-web.github-user-data-provider"},
		"dataProviderContext": map[string]interface{}{
			"properties": map[string]interface{}{
				"accessToken": githubToken,
				"sourcePage": map[string]interface{}{
					"routeValues": map[string]interface{}{
						"project": teamProject,
					},
				},
			},
		},
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return "", fmt.Errorf("get github handle: %w", err)
	}

	if errMsg := extractErrorMessage(resp, "ms.vss-work-web.github-user-data-provider"); errMsg != "" {
		return "", cmdutil.NewUserErrorf("Error validating GitHub token: %s", errMsg)
	}

	var data struct {
		DataProviders map[string]struct {
			Login string `json:"login"`
		} `json:"dataProviders"`
	}
	if err := json.Unmarshal([]byte(resp), &data); err != nil {
		return "", fmt.Errorf("parse github handle response: %w", err)
	}

	dp, ok := data.DataProviders["ms.vss-work-web.github-user-data-provider"]
	if !ok {
		return "", cmdutil.NewUserError("Missing data from 'ms.vss-work-web.github-user-data-provider'. Please ensure the Azure DevOps project has a configured GitHub connection.")
	}
	return dp.Login, nil
}

// GetBoardsGithubConnection returns the first Boards ↔ GitHub external connection.
func (c *Client) GetBoardsGithubConnection(ctx context.Context, org, teamProject string) (BoardsConnection, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org))

	payload := map[string]interface{}{
		"contributionIds": []string{"ms.vss-work-web.azure-boards-external-connection-data-provider"},
		"dataProviderContext": map[string]interface{}{
			"properties": map[string]interface{}{
				"includeInvalidConnections": false,
				"sourcePage": map[string]interface{}{
					"routeValues": map[string]interface{}{
						"project": teamProject,
					},
				},
			},
		},
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return BoardsConnection{}, fmt.Errorf("get boards github connection: %w", err)
	}

	var data struct {
		DataProviders map[string]struct {
			ExternalConnections []struct {
				ID              string `json:"id"`
				Name            string `json:"name"`
				ServiceEndpoint struct {
					ID string `json:"id"`
				} `json:"serviceEndpoint"`
				ExternalGitRepos []struct {
					ID string `json:"id"`
				} `json:"externalGitRepos"`
			} `json:"externalConnections"`
		} `json:"dataProviders"`
	}
	if err := json.Unmarshal([]byte(resp), &data); err != nil {
		return BoardsConnection{}, fmt.Errorf("parse boards connection: %w", err)
	}

	dp, ok := data.DataProviders["ms.vss-work-web.azure-boards-external-connection-data-provider"]
	if !ok || len(dp.ExternalConnections) == 0 {
		return BoardsConnection{}, nil
	}

	conn := dp.ExternalConnections[0]
	repoIDs := make([]string, 0, len(conn.ExternalGitRepos))
	for _, r := range conn.ExternalGitRepos {
		repoIDs = append(repoIDs, r.ID)
	}

	return BoardsConnection{
		ConnectionID:   conn.ID,
		EndpointID:     conn.ServiceEndpoint.ID,
		ConnectionName: conn.Name,
		RepoIDs:        repoIDs,
	}, nil
}

// CreateBoardsGithubEndpoint creates a GitHub boards service endpoint.
func (c *Client) CreateBoardsGithubEndpoint(ctx context.Context, org, teamProjectId, githubToken, githubHandle, endpointName string) (string, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/serviceendpoint/endpoints?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProjectId))

	payload := map[string]interface{}{
		"type": "githubboards",
		"url":  "http://github.com",
		"authorization": map[string]interface{}{
			"scheme": "PersonalAccessToken",
			"parameters": map[string]interface{}{
				"accessToken": githubToken,
			},
		},
		"data": map[string]interface{}{
			"GitHubHandle": githubHandle,
		},
		"name": endpointName,
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return "", fmt.Errorf("create boards github endpoint: %w", err)
	}

	var data struct {
		ID string `json:"id"`
	}
	if err := json.Unmarshal([]byte(resp), &data); err != nil {
		return "", fmt.Errorf("parse endpoint response: %w", err)
	}
	return data.ID, nil
}

// AddRepoToBoardsGithubConnection adds repos to an existing Boards-GitHub connection.
func (c *Client) AddRepoToBoardsGithubConnection(ctx context.Context, org, teamProject, connectionId, connectionName, endpointId string, repoIds []string) error {
	apiURL := fmt.Sprintf("%s/%s/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org))

	payload := map[string]interface{}{
		"contributionIds": []string{"ms.vss-work-web.azure-boards-save-external-connection-data-provider"},
		"dataProviderContext": map[string]interface{}{
			"properties": map[string]interface{}{
				"externalConnection": map[string]interface{}{
					"serviceEndpointId":             endpointId,
					"connectionName":                connectionName,
					"connectionId":                  connectionId,
					"operation":                     1,
					"externalRepositoryExternalIds": repoIds,
					"providerKey":                   "github.com",
					"isGitHubApp":                   false,
				},
				"sourcePage": map[string]interface{}{
					"routeValues": map[string]interface{}{
						"project": teamProject,
					},
				},
			},
		},
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return fmt.Errorf("add repo to boards connection: %w", err)
	}

	if errMsg := extractErrorMessage(resp, "ms.vss-work-web.azure-boards-save-external-connection-data-provider"); errMsg != "" {
		return cmdutil.NewUserErrorf("Error adding repository to boards GitHub connection: %s", errMsg)
	}
	return nil
}

// GetBoardsGithubRepoId returns the GitHub node ID for a repo via HierarchyQuery.
func (c *Client) GetBoardsGithubRepoId(ctx context.Context, org, teamProject, teamProjectId, endpointId, githubOrg, githubRepo string) (string, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org))

	payload := map[string]interface{}{
		"contributionIds": []string{"ms.vss-work-web.github-user-repository-data-provider"},
		"dataProviderContext": map[string]interface{}{
			"properties": map[string]interface{}{
				"projectId":         teamProjectId,
				"repoWithOwnerName": fmt.Sprintf("%s/%s", githubOrg, githubRepo),
				"serviceEndpointId": endpointId,
				"sourcePage": map[string]interface{}{
					"routeValues": map[string]interface{}{
						"project": teamProject,
					},
				},
			},
		},
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return "", fmt.Errorf("get boards github repo id: %w", err)
	}

	if errMsg := extractErrorMessage(resp, "ms.vss-work-web.github-user-repository-data-provider"); errMsg != "" {
		return "", cmdutil.NewUserErrorf("Error getting GitHub repository information: %s", errMsg)
	}

	var data struct {
		DataProviders map[string]struct {
			AdditionalProperties struct {
				NodeID string `json:"nodeId"`
			} `json:"additionalProperties"`
		} `json:"dataProviders"`
	}
	if err := json.Unmarshal([]byte(resp), &data); err != nil {
		return "", fmt.Errorf("parse boards github repo id: %w", err)
	}

	dp, ok := data.DataProviders["ms.vss-work-web.github-user-repository-data-provider"]
	if !ok || dp.AdditionalProperties.NodeID == "" {
		return "", cmdutil.NewUserError("Could not retrieve GitHub repository information. Please verify the repository exists and the GitHub token has the correct permissions.")
	}
	return dp.AdditionalProperties.NodeID, nil
}

// CreateBoardsGithubConnection creates a new Boards-GitHub connection.
func (c *Client) CreateBoardsGithubConnection(ctx context.Context, org, teamProject, endpointId, repoId string) error {
	apiURL := fmt.Sprintf("%s/%s/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1",
		c.baseURL, url.PathEscape(org))

	payload := map[string]interface{}{
		"contributionIds": []string{"ms.vss-work-web.azure-boards-save-external-connection-data-provider"},
		"dataProviderContext": map[string]interface{}{
			"properties": map[string]interface{}{
				"externalConnection": map[string]interface{}{
					"serviceEndpointId":             endpointId,
					"operation":                     0,
					"externalRepositoryExternalIds": []string{repoId},
					"providerKey":                   "github.com",
					"isGitHubApp":                   false,
				},
				"sourcePage": map[string]interface{}{
					"routeValues": map[string]interface{}{
						"project": teamProject,
					},
				},
			},
		},
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return fmt.Errorf("create boards github connection: %w", err)
	}

	if errMsg := extractErrorMessage(resp, "ms.vss-work-web.azure-boards-save-external-connection-data-provider"); errMsg != "" {
		return cmdutil.NewUserErrorf("Error creating boards GitHub connection: %s", errMsg)
	}
	return nil
}

// DisableRepo disables a repository.
func (c *Client) DisableRepo(ctx context.Context, org, teamProject, repoId string) error {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/git/repositories/%s?api-version=6.1-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repoId))

	payload := map[string]interface{}{
		"isDisabled": true,
	}

	_, err := c.patch(ctx, apiURL, payload)
	return err
}

// GetIdentityDescriptor returns the identity descriptor for a security group.
func (c *Client) GetIdentityDescriptor(ctx context.Context, org, teamProjectId, groupName string) (string, error) {
	apiURL := fmt.Sprintf("https://vssps.dev.azure.com/%s/_apis/identities?searchFilter=General&filterValue=%s&queryMembership=None&api-version=6.1-preview.1",
		url.PathEscape(org), url.PathEscape(groupName))

	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return "", fmt.Errorf("get identity descriptor: %w", err)
	}

	for _, raw := range items {
		var ident struct {
			Descriptor string `json:"descriptor"`
			Properties struct {
				LocalScopeId struct {
					Value string `json:"$value"`
				} `json:"LocalScopeId"`
			} `json:"properties"`
		}
		if err := json.Unmarshal(raw, &ident); err != nil {
			continue
		}
		if ident.Properties.LocalScopeId.Value == teamProjectId {
			return ident.Descriptor, nil
		}
	}
	return "", fmt.Errorf("identity descriptor not found for group %q in project %s", groupName, teamProjectId)
}

// LockRepo sets deny permissions on a repo for a given identity.
func (c *Client) LockRepo(ctx context.Context, org, teamProjectId, repoId, identityDescriptor string) error {
	const gitReposNamespace = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87"

	apiURL := fmt.Sprintf("%s/%s/_apis/accesscontrolentries/%s?api-version=6.1-preview.1",
		c.baseURL, url.PathEscape(org), url.PathEscape(gitReposNamespace))

	payload := map[string]interface{}{
		"token": fmt.Sprintf("repoV2/%s/%s", teamProjectId, repoId),
		"merge": true,
		"accessControlEntries": []map[string]interface{}{
			{
				"descriptor": identityDescriptor,
				"allow":      0,
				"deny":       56828,
				"extendedInfo": map[string]interface{}{
					"effectiveAllow": 0,
					"effectiveDeny":  56828,
					"inheritedAllow": 0,
					"inheritedDeny":  56828,
				},
			},
		},
	}

	_, err := c.post(ctx, apiURL, payload)
	return err
}

// IsCallerOrgAdmin checks if the authenticated user has org admin permissions.
func (c *Client) IsCallerOrgAdmin(ctx context.Context, org string) (bool, error) {
	const collectionSecurityNamespaceId = "3e65f728-f8bc-4ecd-8764-7e378b19bfa7"
	const genericWritePermission = 2

	return c.hasPermission(ctx, org, collectionSecurityNamespaceId, genericWritePermission)
}

func (c *Client) hasPermission(ctx context.Context, org, securityNamespaceId string, permission int) (bool, error) {
	apiURL := fmt.Sprintf("%s/%s/_apis/permissions/%s/%d?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(securityNamespaceId), permission)
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return false, fmt.Errorf("check permission: %w", err)
	}

	var data struct {
		Value json.RawMessage `json:"value"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return false, fmt.Errorf("parse permission response: %w", err)
	}

	// The value field can be a bool or the first element might be a bool string
	var boolVal bool
	if err := json.Unmarshal(data.Value, &boolVal); err == nil {
		return boolVal, nil
	}

	// Try parsing as string "true"/"false"
	var strVal string
	if err := json.Unmarshal(data.Value, &strVal); err == nil {
		return strings.EqualFold(strVal, "true"), nil
	}

	return false, nil
}

// GetPipelines returns pipeline paths in "\path\name" format.
func (c *Client) GetPipelines(ctx context.Context, org, teamProject, repoId string) ([]string, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions?repositoryId=%s&repositoryType=TfsGit&queryOrder=lastModifiedDescending",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(repoId))

	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return nil, fmt.Errorf("get pipelines: %w", err)
	}

	var result []string
	for _, raw := range items {
		var def struct {
			Path string `json:"path"`
			Name string `json:"name"`
		}
		if err := json.Unmarshal(raw, &def); err != nil {
			continue
		}
		path := def.Path
		if path == "\\" {
			path = ""
		}
		result = append(result, fmt.Sprintf("%s\\%s", path, def.Name))
	}
	return result, nil
}

// GetPipelineId returns the build definition id for a pipeline, using cache.
func (c *Client) GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error) {
	pipelinePath := normalizePipelinePath(pipeline)
	key := pipelineIDKey{strings.ToUpper(org), strings.ToUpper(teamProject), strings.ToUpper(pipelinePath)}
	if id, ok := c.pipelineIDs[key]; ok {
		return id, nil
	}

	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions?queryOrder=definitionNameAscending",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject))
	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return 0, fmt.Errorf("get pipeline id: %w", err)
	}

	for _, raw := range items {
		var def struct {
			ID   int    `json:"id"`
			Path string `json:"path"`
			Name string `json:"name"`
		}
		if err := json.Unmarshal(raw, &def); err != nil {
			continue
		}
		defPath := normalizePipelinePathParts(def.Path, def.Name)
		defKey := pipelineIDKey{strings.ToUpper(org), strings.ToUpper(teamProject), strings.ToUpper(defPath)}
		if _, exists := c.pipelineIDs[defKey]; exists {
			c.log.Warning("Multiple pipelines with the same path/name were found [org: %s project: %s pipeline: %s]. Ignoring pipeline ID %d", org, teamProject, defPath, def.ID)
			continue
		}
		c.pipelineIDs[defKey] = def.ID
	}

	if id, ok := c.pipelineIDs[key]; ok {
		return id, nil
	}

	// Fallback: try matching by name only if unique
	var matchedID int
	matchCount := 0
	for _, raw := range items {
		var def struct {
			ID   int    `json:"id"`
			Name string `json:"name"`
		}
		if err := json.Unmarshal(raw, &def); err != nil {
			continue
		}
		if strings.EqualFold(def.Name, pipeline) {
			matchedID = def.ID
			matchCount++
		}
	}
	if matchCount == 1 {
		return matchedID, nil
	}

	return 0, fmt.Errorf("unable to find the specified pipeline %q", pipeline)
}

func normalizePipelinePath(pipeline string) string {
	parts := strings.FieldsFunc(pipeline, func(r rune) bool { return r == '\\' })
	return "\\" + strings.Join(parts, "\\")
}

func normalizePipelinePathParts(path, name string) string {
	parts := strings.FieldsFunc(path, func(r rune) bool { return r == '\\' })
	result := strings.Join(parts, "\\")
	if result != "" {
		return "\\" + result + "\\" + name
	}
	return "\\" + name
}

// GetPipeline returns pipeline configuration info.
func (c *Client) GetPipeline(ctx context.Context, org, teamProject string, pipelineId int) (PipelineInfo, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions/%d?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), pipelineId)

	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return PipelineInfo{}, fmt.Errorf("get pipeline: %w", err)
	}

	var data struct {
		Repository struct {
			DefaultBranch      string  `json:"defaultBranch"`
			Clean              *string `json:"clean"`
			CheckoutSubmodules *string `json:"checkoutSubmodules"`
		} `json:"repository"`
		Triggers json.RawMessage `json:"triggers"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return PipelineInfo{}, fmt.Errorf("parse pipeline: %w", err)
	}

	defaultBranch := data.Repository.DefaultBranch
	if strings.HasPrefix(strings.ToLower(defaultBranch), "refs/heads/") {
		defaultBranch = defaultBranch[len("refs/heads/"):]
	}

	clean := nullStr
	if data.Repository.Clean != nil {
		clean = strings.ToLower(*data.Repository.Clean)
	}
	checkout := nullStr
	if data.Repository.CheckoutSubmodules != nil {
		checkout = strings.ToLower(*data.Repository.CheckoutSubmodules)
	}

	return PipelineInfo{
		DefaultBranch:      defaultBranch,
		Clean:              clean,
		CheckoutSubmodules: checkout,
		Triggers:           data.Triggers,
	}, nil
}

// IsPipelineEnabled checks if a pipeline is enabled.
func (c *Client) IsPipelineEnabled(ctx context.Context, org, teamProject string, pipelineId int) (bool, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions/%d?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), pipelineId)

	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return false, fmt.Errorf("check pipeline enabled: %w", err)
	}

	var data struct {
		QueueStatus string `json:"queueStatus"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return false, fmt.Errorf("parse pipeline status: %w", err)
	}

	return data.QueueStatus == "" || strings.EqualFold(data.QueueStatus, "enabled"), nil
}

// GetPipelineRepository returns repository info from a pipeline definition.
func (c *Client) GetPipelineRepository(ctx context.Context, org, teamProject string, pipelineId int) (PipelineRepository, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions/%d?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), pipelineId)

	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return PipelineRepository{}, fmt.Errorf("get pipeline repository: %w", err)
	}

	var data struct {
		Repository struct {
			Name               string  `json:"name"`
			ID                 string  `json:"id"`
			DefaultBranch      string  `json:"defaultBranch"`
			Clean              *string `json:"clean"`
			CheckoutSubmodules *string `json:"checkoutSubmodules"`
		} `json:"repository"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return PipelineRepository{}, fmt.Errorf("parse pipeline repository: %w", err)
	}

	defaultBranch := data.Repository.DefaultBranch
	if strings.HasPrefix(strings.ToLower(defaultBranch), "refs/heads/") {
		defaultBranch = defaultBranch[len("refs/heads/"):]
	}

	clean := nullStr
	if data.Repository.Clean != nil {
		clean = strings.ToLower(*data.Repository.Clean)
	}
	checkout := nullStr
	if data.Repository.CheckoutSubmodules != nil {
		checkout = strings.ToLower(*data.Repository.CheckoutSubmodules)
	}

	return PipelineRepository{
		RepoName:           data.Repository.Name,
		RepoID:             data.Repository.ID,
		DefaultBranch:      defaultBranch,
		Clean:              clean,
		CheckoutSubmodules: checkout,
	}, nil
}

// RestorePipelineToAdoRepo restores a pipeline definition to use an ADO repository.
func (c *Client) RestorePipelineToAdoRepo(ctx context.Context, org, teamProject string, pipelineId int, adoRepoName, defaultBranch, clean, checkoutSubmodules string, originalTriggers json.RawMessage) error {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/definitions/%d?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), pipelineId)

	// GET the current definition
	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return fmt.Errorf("get pipeline definition: %w", err)
	}

	// Get repo id
	adoRepoId, err := c.GetRepoId(ctx, org, teamProject, adoRepoName)
	if err != nil {
		return fmt.Errorf("get repo id for restore: %w", err)
	}

	// Parse the existing definition and modify it
	var data map[string]json.RawMessage
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return fmt.Errorf("parse pipeline definition: %w", err)
	}

	// Build the ADO repo object
	adoRepo := map[string]interface{}{
		"id":                 adoRepoId,
		"type":               "TfsGit",
		"name":               adoRepoName,
		"url":                fmt.Sprintf("%s/%s/%s/_git/%s", c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), url.PathEscape(adoRepoName)),
		"defaultBranch":      defaultBranch,
		"clean":              clean,
		"checkoutSubmodules": checkoutSubmodules,
		"properties": map[string]interface{}{
			"cleanOptions":             "0",
			"labelSources":             "0",
			"labelSourcesFormat":       "$(build.buildNumber)",
			"reportBuildStatus":        "true",
			"gitLfsSupport":            "false",
			"skipSyncSource":           "false",
			"checkoutNestedSubmodules": "false",
			"fetchDepth":               "0",
		},
	}

	repoJSON, _ := json.Marshal(adoRepo)
	data["repository"] = repoJSON

	if originalTriggers != nil {
		data["triggers"] = originalTriggers
	}

	// Restore settingsSourceType to 1 (UI-controlled)
	settingsJSON, _ := json.Marshal(1)
	data["settingsSourceType"] = settingsJSON

	_, err = c.put(ctx, apiURL, data)
	return err
}

// QueueBuild queues a new build.
func (c *Client) QueueBuild(ctx context.Context, org, teamProject string, pipelineId int, sourceBranch string) (int, error) {
	if sourceBranch == "" {
		sourceBranch = "refs/heads/main"
	}

	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/builds?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject))

	payload := map[string]interface{}{
		"definition":   map[string]interface{}{"id": pipelineId},
		"sourceBranch": sourceBranch,
		"reason":       "manual",
	}

	resp, err := c.post(ctx, apiURL, payload)
	if err != nil {
		return 0, fmt.Errorf("queue build: %w", err)
	}

	var data struct {
		ID int `json:"id"`
	}
	if err := json.Unmarshal([]byte(resp), &data); err != nil {
		return 0, fmt.Errorf("parse build response: %w", err)
	}
	return data.ID, nil
}

// GetBuildStatus returns the status of a specific build.
func (c *Client) GetBuildStatus(ctx context.Context, org, teamProject string, buildId int) (BuildStatus, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/builds/%d?api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), buildId)

	body, _, err := c.get(ctx, apiURL)
	if err != nil {
		return BuildStatus{}, fmt.Errorf("get build status: %w", err)
	}

	var data struct {
		Status string `json:"status"`
		Result string `json:"result"`
		Links  struct {
			Web struct {
				Href string `json:"href"`
			} `json:"web"`
		} `json:"_links"`
	}
	if err := json.Unmarshal([]byte(body), &data); err != nil {
		return BuildStatus{}, fmt.Errorf("parse build status: %w", err)
	}

	return BuildStatus{
		Status: data.Status,
		Result: data.Result,
		URL:    data.Links.Web.Href,
	}, nil
}

// GetBuilds returns builds for a pipeline definition.
func (c *Client) GetBuilds(ctx context.Context, org, teamProject string, pipelineId int, minTime *time.Time) ([]Build, error) {
	apiURL := fmt.Sprintf("%s/%s/%s/_apis/build/builds?definitions=%d&api-version=6.0",
		c.baseURL, url.PathEscape(org), url.PathEscape(teamProject), pipelineId)

	if minTime != nil {
		apiURL += fmt.Sprintf("&minTime=%s", minTime.Format("2006-01-02T15:04:05.000Z"))
	}

	items, err := c.getWithPaging(ctx, apiURL)
	if err != nil {
		return nil, fmt.Errorf("get builds: %w", err)
	}

	var builds []Build
	for _, raw := range items {
		var b struct {
			ID        int       `json:"id"`
			Status    string    `json:"status"`
			Result    string    `json:"result"`
			QueueTime time.Time `json:"queueTime"`
			Links     struct {
				Web struct {
					Href string `json:"href"`
				} `json:"web"`
			} `json:"_links"`
		}
		if err := json.Unmarshal(raw, &b); err != nil {
			continue
		}
		builds = append(builds, Build{
			BuildID:   b.ID,
			Status:    b.Status,
			Result:    b.Result,
			URL:       b.Links.Web.Href,
			QueueTime: b.QueueTime,
		})
	}
	return builds, nil
}
