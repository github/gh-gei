package status

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
)

// statusResponse is the minimal shape of the GitHub status API response.
type statusResponse struct {
	Incidents []json.RawMessage `json:"incidents"`
}

// GetUnresolvedIncidentsCount fetches the count of unresolved GitHub incidents.
// baseURL allows overriding the status API base URL for testing.
func GetUnresolvedIncidentsCount(ctx context.Context, client *http.Client, baseURL string) (int, error) {
	url := baseURL + "/api/v2/incidents/unresolved.json"

	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return 0, fmt.Errorf("creating status request: %w", err)
	}

	resp, err := client.Do(req)
	if err != nil {
		return 0, fmt.Errorf("fetching GitHub status: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return 0, fmt.Errorf("GitHub status API returned status %d", resp.StatusCode)
	}

	body, err := io.ReadAll(io.LimitReader(resp.Body, 1<<20)) // 1MB limit
	if err != nil {
		return 0, fmt.Errorf("reading status response: %w", err)
	}

	var result statusResponse
	if err := json.Unmarshal(body, &result); err != nil {
		return 0, fmt.Errorf("parsing status response: %w", err)
	}

	return len(result.Incidents), nil
}
