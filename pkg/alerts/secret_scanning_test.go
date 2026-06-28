package alerts_test

import (
	"bytes"
	"context"
	"fmt"
	"testing"

	"github.com/github/gh-gei/pkg/alerts"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockSecretScanningAPI implements alerts.SecretScanningGitHubAPI for testing.
type mockSecretScanningAPI struct {
	alerts    []github.SecretScanningAlert
	locations map[int][]github.SecretScanningAlertLocation // keyed by alert number
	updates   []secretAlertUpdate
	alertsErr error
	locsErr   error
	updateErr error
}

type secretAlertUpdate struct {
	Org, Repo         string
	AlertNumber       int
	State, Resolution string
	Comment           string
}

func (m *mockSecretScanningAPI) GetSecretScanningAlertsForRepository(_ context.Context, org, repo string) ([]github.SecretScanningAlert, error) {
	if m.alertsErr != nil {
		return nil, m.alertsErr
	}
	return m.alerts, nil
}

func (m *mockSecretScanningAPI) GetSecretScanningAlertsLocations(_ context.Context, org, repo string, alertNumber int) ([]github.SecretScanningAlertLocation, error) {
	if m.locsErr != nil {
		return nil, m.locsErr
	}
	return m.locations[alertNumber], nil
}

func (m *mockSecretScanningAPI) UpdateSecretScanningAlert(_ context.Context, org, repo string, alertNumber int, state, resolution, resolutionComment string) error {
	if m.updateErr != nil {
		return m.updateErr
	}
	m.updates = append(m.updates, secretAlertUpdate{
		Org: org, Repo: repo, AlertNumber: alertNumber,
		State: state, Resolution: resolution, Comment: resolutionComment,
	})
	return nil
}

func TestSecretScanningService_MigrateAlerts(t *testing.T) {
	t.Run("skips open source alerts", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts:    []github.SecretScanningAlert{},
			locations: map[int][]github.SecretScanningAlertLocation{},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		assert.Empty(t, target.updates)
		assert.Contains(t, buf.String(), "still open")
	})

	t.Run("matches and updates resolved alert by commit location", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", ResolutionComment: "fixed it", SecretType: "github_token", Secret: "ghp_abc", ResolverName: "alice"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "config.yml", StartLine: 10, EndLine: 10, StartColumn: 1, EndColumn: 40, BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 5, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				5: {{LocationType: "commit", Path: "config.yml", StartLine: 10, EndLine: 10, StartColumn: 1, EndColumn: 40, BlobSha: "sha1"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		require.Len(t, target.updates, 1)
		assert.Equal(t, 5, target.updates[0].AlertNumber)
		assert.Equal(t, "resolved", target.updates[0].State)
		assert.Equal(t, "revoked", target.updates[0].Resolution)
		assert.Equal(t, "[@alice] fixed it", target.updates[0].Comment)
	})

	t.Run("skips already aligned alerts", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 5, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				5: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		assert.Empty(t, target.updates)
		assert.Contains(t, buf.String(), "already aligned")
	})

	t.Run("dry run does not update", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 5, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				5: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", true)

		require.NoError(t, err)
		assert.Empty(t, target.updates)
		assert.Contains(t, buf.String(), "dry run")
	})

	t.Run("warns when no matching secret type/secret key", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts:    []github.SecretScanningAlert{},
			locations: map[int][]github.SecretScanningAlertLocation{},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		assert.Empty(t, target.updates)
		assert.Contains(t, buf.String(), "Failed to locate")
	})

	t.Run("warns when locations do not match", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 5, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				5: {{LocationType: "commit", Path: "DIFFERENT.txt", BlobSha: "sha2"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		assert.Empty(t, target.updates)
		assert.Contains(t, buf.String(), "failed to locate")
	})

	t.Run("matches issue_title location by URL final segment", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc", ResolverName: "bob"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "issue_title", IssueTitleUrl: "https://api.github.com/repos/src-org/src-repo/issues/42"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 10, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				10: {{LocationType: "issue_title", IssueTitleUrl: "https://api.github.com/repos/tgt-org/tgt-repo/issues/42"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		require.Len(t, target.updates, 1)
		assert.Equal(t, 10, target.updates[0].AlertNumber)
	})

	t.Run("truncates resolution comment to 270 chars", func(t *testing.T) {
		longComment := ""
		for i := 0; i < 300; i++ {
			longComment += "x"
		}
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", ResolutionComment: longComment, SecretType: "github_token", Secret: "ghp_abc", ResolverName: "alice"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 5, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				5: {{LocationType: "commit", Path: "f.txt", BlobSha: "sha1"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		require.Len(t, target.updates, 1)
		assert.LessOrEqual(t, len(target.updates[0].Comment), 270)
		assert.True(t, len(target.updates[0].Comment) > 0)
		assert.Contains(t, target.updates[0].Comment, "[@alice]")
	})

	t.Run("multiple alerts with same secret type/secret", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 1, State: "resolved", Resolution: "revoked", SecretType: "github_token", Secret: "ghp_abc", ResolverName: "alice"},
				{Number: 2, State: "resolved", Resolution: "false_positive", SecretType: "github_token", Secret: "ghp_abc", ResolverName: "bob"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				1: {{LocationType: "commit", Path: "a.txt", BlobSha: "sha1"}},
				2: {{LocationType: "commit", Path: "b.txt", BlobSha: "sha2"}},
			},
		}
		target := &mockSecretScanningAPI{
			alerts: []github.SecretScanningAlert{
				{Number: 10, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
				{Number: 11, State: "open", SecretType: "github_token", Secret: "ghp_abc"},
			},
			locations: map[int][]github.SecretScanningAlertLocation{
				10: {{LocationType: "commit", Path: "a.txt", BlobSha: "sha1"}},
				11: {{LocationType: "commit", Path: "b.txt", BlobSha: "sha2"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		require.Len(t, target.updates, 2)
		// Check both alerts got updated
		nums := []int{target.updates[0].AlertNumber, target.updates[1].AlertNumber}
		assert.Contains(t, nums, 10)
		assert.Contains(t, nums, 11)
	})

	t.Run("returns error when source fetch fails", func(t *testing.T) {
		source := &mockSecretScanningAPI{
			alertsErr: fmt.Errorf("API error"),
		}
		target := &mockSecretScanningAPI{}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewSecretScanningService(source, target, log)
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "API error")
	})
}
