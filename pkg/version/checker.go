package version

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"strconv"
	"strings"

	"github.com/github/gh-gei/pkg/logger"
)

const defaultVersionURL = "https://raw.githubusercontent.com/github/gh-gei/main/LATEST-VERSION.txt"

// semver represents a simple major.minor.patch version.
type semver struct {
	major, minor, patch int
}

func (v semver) compare(other semver) int {
	if v.major != other.major {
		if v.major > other.major {
			return 1
		}
		return -1
	}
	if v.minor != other.minor {
		if v.minor > other.minor {
			return 1
		}
		return -1
	}
	if v.patch != other.patch {
		if v.patch > other.patch {
			return 1
		}
		return -1
	}
	return 0
}

// parseVersion parses a version string like "v1.27.0" into a semver.
func parseVersion(s string) (semver, error) {
	s = strings.TrimSpace(s)
	s = strings.TrimPrefix(s, "v")
	s = strings.TrimPrefix(s, "V")

	parts := strings.Split(s, ".")
	if len(parts) != 3 {
		return semver{}, fmt.Errorf("invalid version format: %q (expected major.minor.patch)", s)
	}

	major, err := strconv.Atoi(parts[0])
	if err != nil {
		return semver{}, fmt.Errorf("invalid major version %q: %w", parts[0], err)
	}
	minor, err := strconv.Atoi(parts[1])
	if err != nil {
		return semver{}, fmt.Errorf("invalid minor version %q: %w", parts[1], err)
	}
	patch, err := strconv.Atoi(parts[2])
	if err != nil {
		return semver{}, fmt.Errorf("invalid patch version %q: %w", parts[2], err)
	}

	return semver{major, minor, patch}, nil
}

// Checker checks whether the current CLI version is the latest.
type Checker struct {
	httpClient    *http.Client
	logger        *logger.Logger
	version       string
	latestVersion *string // cached
	versionURL    string
}

// NewChecker creates a new version Checker.
func NewChecker(httpClient *http.Client, log *logger.Logger, version string) *Checker {
	return &Checker{
		httpClient: httpClient,
		logger:     log,
		version:    version,
		versionURL: defaultVersionURL,
	}
}

// GetLatestVersion fetches the latest version string from GitHub.
func (c *Checker) GetLatestVersion(ctx context.Context) (string, error) {
	if c.latestVersion != nil {
		return *c.latestVersion, nil
	}

	req, err := http.NewRequestWithContext(ctx, http.MethodGet, c.versionURL, nil)
	if err != nil {
		return "", fmt.Errorf("creating version check request: %w", err)
	}
	req.Header.Set("User-Agent", "OctoshiftCLI/"+c.version)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return "", fmt.Errorf("fetching latest version: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("version check returned status %d", resp.StatusCode)
	}

	body, err := io.ReadAll(io.LimitReader(resp.Body, 1024)) // version file is tiny
	if err != nil {
		return "", fmt.Errorf("reading version response: %w", err)
	}

	raw := strings.TrimSpace(string(body))

	// Validate it parses (parseVersion handles v/V prefix stripping)
	v, err := parseVersion(raw)
	if err != nil {
		return "", err
	}

	// Cache the normalized version string (no prefix)
	normalized := fmt.Sprintf("%d.%d.%d", v.major, v.minor, v.patch)
	c.latestVersion = &normalized
	return normalized, nil
}

// IsLatest returns true if the current version is >= the latest published version.
func (c *Checker) IsLatest(ctx context.Context) (bool, error) {
	latestStr, err := c.GetLatestVersion(ctx)
	if err != nil {
		return false, err
	}

	current, err := parseVersion(c.version)
	if err != nil {
		return false, fmt.Errorf("parsing current version: %w", err)
	}

	latest, err := parseVersion(latestStr)
	if err != nil {
		return false, fmt.Errorf("parsing latest version: %w", err)
	}

	return current.compare(latest) >= 0, nil
}
