package github

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/github/gh-gei/pkg/logger"
)

const (
	graphqlPath          = "/graphql"
	graphqlFeaturesValue = "import_api,mannequin_claiming_emu,org_import_api"
	maxRetries           = 3
	defaultPageSize      = 100
)

// graphqlClient is an internal GraphQL client for GitHub's migration API.
// It handles auth, required headers, pagination, and secondary rate limiting.
type graphqlClient struct {
	httpClient *http.Client
	baseURL    string
	pat        string
	version    string
	logger     *logger.Logger
}

// graphqlRequest is the JSON body sent to the GraphQL endpoint.
type graphqlRequest struct {
	Query     string          `json:"query"`
	Variables json.RawMessage `json:"variables,omitempty"`
}

// graphqlResponse is the top-level response from the GraphQL endpoint.
type graphqlResponse struct {
	Data   json.RawMessage `json:"data"`
	Errors []graphqlError  `json:"errors"`
}

type graphqlError struct {
	Message string `json:"message"`
	Type    string `json:"type,omitempty"`
}

// pageInfo mirrors the GraphQL PageInfo type.
type pageInfo struct {
	HasNextPage bool   `json:"hasNextPage"`
	EndCursor   string `json:"endCursor"`
}

func newGraphQLClient(baseURL, pat, version string, log *logger.Logger) *graphqlClient {
	return &graphqlClient{
		httpClient: &http.Client{Timeout: 30 * time.Second},
		baseURL:    strings.TrimRight(baseURL, "/"),
		pat:        pat,
		version:    version,
		logger:     log,
	}
}

// Post sends a GraphQL query and returns the raw "data" field.
func (c *graphqlClient) Post(ctx context.Context, query string, variables json.RawMessage) (json.RawMessage, error) {
	return c.doWithRetry(ctx, query, variables, 0)
}

func (c *graphqlClient) doWithRetry(ctx context.Context, query string, variables json.RawMessage, retryCount int) (json.RawMessage, error) {
	reqBody := graphqlRequest{
		Query:     query,
		Variables: variables,
	}
	bodyBytes, err := json.Marshal(reqBody)
	if err != nil {
		return nil, fmt.Errorf("graphql: failed to marshal request: %w", err)
	}

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, c.baseURL+graphqlPath, bytes.NewReader(bodyBytes))
	if err != nil {
		return nil, fmt.Errorf("graphql: failed to create request: %w", err)
	}

	req.Header.Set("Authorization", "Bearer "+c.pat)
	req.Header.Set("GraphQL-Features", graphqlFeaturesValue)
	req.Header.Set("User-Agent", "OctoshiftCLI/"+c.version)
	req.Header.Set("Content-Type", "application/json")

	c.logger.Debug("GraphQL POST: %s", c.baseURL+graphqlPath)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("graphql: request failed: %w", err)
	}
	defer func() { _ = resp.Body.Close() }()

	respBody, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, fmt.Errorf("graphql: failed to read response: %w", err)
	}

	// Check for secondary rate limit before checking status
	if isSecondaryRateLimit(resp.StatusCode, string(respBody)) {
		if retryCount >= maxRetries {
			return nil, fmt.Errorf("graphql: secondary rate limit exceeded after %d retries", maxRetries)
		}
		delay := computeBackoff(resp, retryCount)
		c.logger.Warning("Secondary rate limit hit, retrying in %v (attempt %d/%d)", delay, retryCount+1, maxRetries)
		select {
		case <-ctx.Done():
			return nil, ctx.Err()
		case <-time.After(delay):
		}
		return c.doWithRetry(ctx, query, variables, retryCount+1)
	}

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return nil, fmt.Errorf("graphql: HTTP %d: %s", resp.StatusCode, string(respBody))
	}

	var gqlResp graphqlResponse
	if err := json.Unmarshal(respBody, &gqlResp); err != nil {
		return nil, fmt.Errorf("graphql: failed to parse response: %w", err)
	}

	if len(gqlResp.Errors) > 0 {
		return nil, fmt.Errorf("graphql: %s", gqlResp.Errors[0].Message)
	}

	return gqlResp.Data, nil
}

// PostWithPagination sends a paginated GraphQL query, collecting all pages.
// dataPath is a dot-separated path to the array in the data (e.g. "organization.repositories.nodes").
// pageInfoPath is a dot-separated path to the pageInfo object (e.g. "organization.repositories.pageInfo").
func (c *graphqlClient) PostWithPagination(
	ctx context.Context,
	query string,
	variables json.RawMessage,
	dataPath string,
	pageInfoPath string,
) (json.RawMessage, error) {
	var allItems []json.RawMessage
	var cursor *string

	for {
		// Inject first and after into variables
		vars, err := injectPaginationVars(variables, defaultPageSize, cursor)
		if err != nil {
			return nil, fmt.Errorf("graphql pagination: failed to inject variables: %w", err)
		}

		data, err := c.Post(ctx, query, vars)
		if err != nil {
			return nil, err
		}

		// Navigate to the data array
		items, err := navigateJSON(data, dataPath)
		if err != nil {
			return nil, fmt.Errorf("graphql pagination: failed to navigate data path %q: %w", dataPath, err)
		}

		// Parse items as array
		var pageItems []json.RawMessage
		if err := json.Unmarshal(items, &pageItems); err != nil {
			return nil, fmt.Errorf("graphql pagination: data at path %q is not an array: %w", dataPath, err)
		}
		allItems = append(allItems, pageItems...)

		// Navigate to pageInfo
		piRaw, err := navigateJSON(data, pageInfoPath)
		if err != nil {
			return nil, fmt.Errorf("graphql pagination: failed to navigate pageInfo path %q: %w", pageInfoPath, err)
		}

		var pi pageInfo
		if err := json.Unmarshal(piRaw, &pi); err != nil {
			return nil, fmt.Errorf("graphql pagination: failed to parse pageInfo: %w", err)
		}

		if !pi.HasNextPage {
			break
		}
		cursor = &pi.EndCursor
	}

	result, err := json.Marshal(allItems)
	if err != nil {
		return nil, fmt.Errorf("graphql pagination: failed to marshal results: %w", err)
	}
	return result, nil
}

// injectPaginationVars merges "first" and "after" into the variables map.
func injectPaginationVars(variables json.RawMessage, first int, after *string) (json.RawMessage, error) {
	var vars map[string]interface{}
	if len(variables) > 0 {
		if err := json.Unmarshal(variables, &vars); err != nil {
			return nil, err
		}
	}
	if vars == nil {
		vars = make(map[string]interface{})
	}
	vars["first"] = first
	if after != nil {
		vars["after"] = *after
	}
	return json.Marshal(vars)
}

// navigateJSON walks a dot-separated path through a JSON object.
func navigateJSON(data json.RawMessage, path string) (json.RawMessage, error) {
	parts := strings.Split(path, ".")
	current := data
	for _, part := range parts {
		var obj map[string]json.RawMessage
		if err := json.Unmarshal(current, &obj); err != nil {
			return nil, fmt.Errorf("expected object at %q: %w", part, err)
		}
		val, ok := obj[part]
		if !ok {
			return nil, fmt.Errorf("key %q not found", part)
		}
		current = val
	}
	return current, nil
}

// isSecondaryRateLimit checks whether the response indicates a secondary rate limit.
// It returns true for:
//   - Any 429 (unless body contains "API RATE LIMIT EXCEEDED")
//   - 403 with body containing "SECONDARY RATE LIMIT" or "ABUSE DETECTION"
//
// It excludes primary rate limits (403 with "API RATE LIMIT EXCEEDED").
func isSecondaryRateLimit(statusCode int, body string) bool {
	upper := strings.ToUpper(body)

	// Primary rate limit — never retry
	if strings.Contains(upper, "API RATE LIMIT EXCEEDED") {
		return false
	}

	if statusCode == http.StatusTooManyRequests {
		return true
	}

	if statusCode == http.StatusForbidden {
		return strings.Contains(upper, "SECONDARY RATE LIMIT") || strings.Contains(upper, "ABUSE DETECTION")
	}

	return false
}

// computeBackoff determines how long to wait before retrying.
// Priority: Retry-After header → X-RateLimit-Reset → exponential 60*2^retryCount.
func computeBackoff(resp *http.Response, retryCount int) time.Duration {
	// Try Retry-After header (seconds)
	if ra := resp.Header.Get("Retry-After"); ra != "" {
		if seconds, err := strconv.Atoi(ra); err == nil {
			return time.Duration(seconds) * time.Second
		}
	}

	// Try X-RateLimit-Reset (unix timestamp)
	if reset := resp.Header.Get("X-RateLimit-Reset"); reset != "" {
		if ts, err := strconv.ParseInt(reset, 10, 64); err == nil {
			resetTime := time.Unix(ts, 0)
			delay := time.Until(resetTime)
			if delay > 0 {
				return delay
			}
		}
	}

	// Exponential backoff: 60 * 2^retryCount seconds
	return time.Duration(60*(1<<retryCount)) * time.Second
}
