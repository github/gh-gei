package ghes_test

import (
	"context"
	"errors"
	"testing"

	"github.com/github/gh-gei/pkg/ghes"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockVersionFetcher implements ghes.VersionFetcher for testing.
type mockVersionFetcher struct {
	version string
	err     error
}

func (m *mockVersionFetcher) GetEnterpriseServerVersion(_ context.Context) (string, error) {
	return m.version, m.err
}

func TestAreBlobCredentialsRequired(t *testing.T) {
	ctx := context.Background()

	t.Run("empty GHES URL returns false", func(t *testing.T) {
		log := logger.New(false)
		checker := ghes.NewVersionChecker(&mockVersionFetcher{}, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "")

		require.NoError(t, err)
		assert.False(t, result)
	})

	t.Run("older GHES version returns true", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{version: "3.7.1"}
		checker := ghes.NewVersionChecker(api, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		require.NoError(t, err)
		assert.True(t, result)
	})

	t.Run("GHES 3.8.0 returns false", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{version: "3.8.0"}
		checker := ghes.NewVersionChecker(api, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		require.NoError(t, err)
		assert.False(t, result)
	})

	t.Run("newer GHES version returns false", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{version: "3.9.1"}
		checker := ghes.NewVersionChecker(api, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		require.NoError(t, err)
		assert.False(t, result)
	})

	t.Run("empty version string returns true", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{version: ""}
		checker := ghes.NewVersionChecker(api, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		require.NoError(t, err)
		assert.True(t, result)
	})

	t.Run("unparseable version returns true", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{version: "Github AE"}
		checker := ghes.NewVersionChecker(api, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		require.NoError(t, err)
		assert.True(t, result)
	})

	t.Run("API error propagates", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{err: errors.New("connection refused")}
		checker := ghes.NewVersionChecker(api, log)

		_, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		assert.Error(t, err)
		assert.Contains(t, err.Error(), "connection refused")
	})

	t.Run("GHES version 3.7.0 returns true", func(t *testing.T) {
		log := logger.New(false)
		api := &mockVersionFetcher{version: "3.7.0"}
		checker := ghes.NewVersionChecker(api, log)

		result, err := checker.AreBlobCredentialsRequired(ctx, "https://ghes.contoso.com/api/v3")

		require.NoError(t, err)
		assert.True(t, result)
	})
}
