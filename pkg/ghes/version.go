// Package ghes provides utilities for working with GitHub Enterprise Server.
package ghes

import (
	"context"
	"fmt"
	"strconv"
	"strings"

	"github.com/github/gh-gei/pkg/logger"
)

// VersionFetcher retrieves the GHES version from the API.
type VersionFetcher interface {
	GetEnterpriseServerVersion(ctx context.Context) (string, error)
}

// VersionChecker determines whether external blob storage credentials are
// required for the given GHES instance.
type VersionChecker struct {
	api    VersionFetcher
	logger *logger.Logger
}

// NewVersionChecker creates a new VersionChecker.
func NewVersionChecker(api VersionFetcher, log *logger.Logger) *VersionChecker {
	return &VersionChecker{api: api, logger: log}
}

// AreBlobCredentialsRequired returns true if the GHES version requires
// external blob storage (Azure/AWS) for migration archives.
// Versions < 3.8.0 require external storage; >= 3.8.0 can use GitHub-owned storage.
// If ghesAPIURL is empty, the target is github.com and no blob credentials are needed.
func (v *VersionChecker) AreBlobCredentialsRequired(ctx context.Context, ghesAPIURL string) (bool, error) {
	if strings.TrimSpace(ghesAPIURL) == "" {
		return false, nil
	}

	v.logger.Info("Using GitHub Enterprise Server - verifying server version")

	ghesVersion, err := v.api.GetEnterpriseServerVersion(ctx)
	if err != nil {
		return false, fmt.Errorf("getting GHES version: %w", err)
	}

	if ghesVersion == "" {
		return true, nil
	}

	v.logger.Info("GitHub Enterprise Server version %s detected", ghesVersion)

	major, minor, patch, parseErr := parseGHESVersion(ghesVersion)
	if parseErr != nil {
		v.logger.Info("Unable to parse the version number, defaulting to using CLI for blob storage uploads")
		return true, nil
	}

	threshold := [3]int{3, 8, 0}
	version := [3]int{major, minor, patch}

	return version[0] < threshold[0] ||
		(version[0] == threshold[0] && version[1] < threshold[1]) ||
		(version[0] == threshold[0] && version[1] == threshold[1] && version[2] < threshold[2]), nil
}

// parseGHESVersion parses a "major.minor.patch" version string.
func parseGHESVersion(s string) (major, minor, patch int, err error) {
	parts := strings.Split(strings.TrimSpace(s), ".")
	if len(parts) != 3 {
		return 0, 0, 0, fmt.Errorf("invalid version format: %q", s)
	}

	major, err = strconv.Atoi(parts[0])
	if err != nil {
		return 0, 0, 0, fmt.Errorf("invalid major version %q: %w", parts[0], err)
	}
	minor, err = strconv.Atoi(parts[1])
	if err != nil {
		return 0, 0, 0, fmt.Errorf("invalid minor version %q: %w", parts[1], err)
	}
	patch, err = strconv.Atoi(parts[2])
	if err != nil {
		return 0, 0, 0, fmt.Errorf("invalid patch version %q: %w", parts[2], err)
	}

	return major, minor, patch, nil
}
