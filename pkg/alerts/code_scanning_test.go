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

// mockCodeScanningAPI implements alerts.CodeScanningGitHubAPI for testing.
type mockCodeScanningAPI struct {
	defaultBranch       string
	defaultBranchErr    error
	alerts              []github.CodeScanningAlert
	alertsErr           error
	instances           map[int][]github.CodeScanningAlertInstance // keyed by alert number
	instancesErr        error
	analyses            []github.CodeScanningAnalysis
	analysesErr         error
	sarifReports        map[int]string // keyed by analysis ID
	sarifReportErr      error
	uploadedSarifs      []sarifUpload
	uploadErr           error
	uploadID            string
	processingStatuses  []*github.SarifProcessingStatus // returned in order
	processingStatusIdx int
	processingErr       error
	alertUpdates        []codeAlertUpdate
	updateErr           error
}

type sarifUpload struct {
	Org, Repo, Sarif, CommitSha, Ref string
}

type codeAlertUpdate struct {
	Org, Repo        string
	AlertNumber      int
	State            string
	DismissedReason  string
	DismissedComment string
}

func (m *mockCodeScanningAPI) GetDefaultBranch(_ context.Context, _, _ string) (string, error) {
	if m.defaultBranchErr != nil {
		return "", m.defaultBranchErr
	}
	return m.defaultBranch, nil
}

func (m *mockCodeScanningAPI) GetCodeScanningAlertsForRepository(_ context.Context, _, _ string, _ string) ([]github.CodeScanningAlert, error) {
	if m.alertsErr != nil {
		return nil, m.alertsErr
	}
	return m.alerts, nil
}

func (m *mockCodeScanningAPI) GetCodeScanningAlertInstances(_ context.Context, _, _ string, alertNumber int) ([]github.CodeScanningAlertInstance, error) {
	if m.instancesErr != nil {
		return nil, m.instancesErr
	}
	return m.instances[alertNumber], nil
}

func (m *mockCodeScanningAPI) GetCodeScanningAnalysisForRepository(_ context.Context, _, _, _ string) ([]github.CodeScanningAnalysis, error) {
	if m.analysesErr != nil {
		return nil, m.analysesErr
	}
	return m.analyses, nil
}

func (m *mockCodeScanningAPI) GetSarifReport(_ context.Context, _, _ string, analysisID int) (string, error) {
	if m.sarifReportErr != nil {
		return "", m.sarifReportErr
	}
	return m.sarifReports[analysisID], nil
}

func (m *mockCodeScanningAPI) UploadSarifReport(_ context.Context, org, repo, sarif, commitSha, ref string) (string, error) {
	if m.uploadErr != nil {
		return "", m.uploadErr
	}
	m.uploadedSarifs = append(m.uploadedSarifs, sarifUpload{
		Org: org, Repo: repo, Sarif: sarif, CommitSha: commitSha, Ref: ref,
	})
	return m.uploadID, nil
}

func (m *mockCodeScanningAPI) GetSarifProcessingStatus(_ context.Context, _, _, _ string) (*github.SarifProcessingStatus, error) {
	if m.processingErr != nil {
		return nil, m.processingErr
	}
	if m.processingStatusIdx < len(m.processingStatuses) {
		s := m.processingStatuses[m.processingStatusIdx]
		m.processingStatusIdx++
		return s, nil
	}
	return &github.SarifProcessingStatus{Status: "complete"}, nil
}

func (m *mockCodeScanningAPI) UpdateCodeScanningAlert(_ context.Context, org, repo string, alertNumber int, state, dismissedReason, dismissedComment string) error {
	if m.updateErr != nil {
		return m.updateErr
	}
	m.alertUpdates = append(m.alertUpdates, codeAlertUpdate{
		Org: org, Repo: repo, AlertNumber: alertNumber,
		State: state, DismissedReason: dismissedReason, DismissedComment: dismissedComment,
	})
	return nil
}

func newInst(ref, sha, path string, startLine, endLine, startCol, endCol int) *github.CodeScanningAlertInstance {
	return &github.CodeScanningAlertInstance{
		Ref: ref, CommitSha: sha, Path: path,
		StartLine: startLine, EndLine: endLine, StartColumn: startCol, EndColumn: endCol,
	}
}

// ---------------------------------------------------------------------------
// MigrateAnalyses tests
// ---------------------------------------------------------------------------

func TestCodeScanningService_MigrateAnalyses(t *testing.T) {
	t.Run("uploads SARIF for analyses not on target", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 1, Ref: "refs/heads/main", CommitSha: "sha1", CreatedAt: "2024-01-01"},
				{ID: 2, Ref: "refs/heads/main", CommitSha: "sha2", CreatedAt: "2024-01-02"},
			},
			sarifReports: map[int]string{
				1: `{"sarif":"report1"}`,
				2: `{"sarif":"report2"}`,
			},
		}
		target := &mockCodeScanningAPI{
			analyses:           []github.CodeScanningAnalysis{},
			uploadID:           "upload-1",
			processingStatuses: []*github.SarifProcessingStatus{{Status: "complete"}, {Status: "complete"}},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAnalyses(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		require.Len(t, target.uploadedSarifs, 2)
		assert.Equal(t, "sha1", target.uploadedSarifs[0].CommitSha)
		assert.Equal(t, "sha2", target.uploadedSarifs[1].CommitSha)
	})

	t.Run("skips analyses already on target", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 1, Ref: "refs/heads/main", CommitSha: "sha1"},
				{ID: 2, Ref: "refs/heads/main", CommitSha: "sha2"},
				{ID: 3, Ref: "refs/heads/main", CommitSha: "sha3"},
			},
			sarifReports: map[int]string{
				3: `{"sarif":"report3"}`,
			},
		}
		target := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 100}, // 2 already on target
				{ID: 101},
			},
			uploadID:           "upload-1",
			processingStatuses: []*github.SarifProcessingStatus{{Status: "complete"}},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAnalyses(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		require.Len(t, target.uploadedSarifs, 1)
		assert.Equal(t, "sha3", target.uploadedSarifs[0].CommitSha)
		assert.Contains(t, buf.String(), "2 of 3 source analyses will be skipped")
	})

	t.Run("dry run does not upload", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 1, Ref: "refs/heads/main", CommitSha: "sha1", CreatedAt: "2024-01-01"},
			},
		}
		target := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAnalyses(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", true)

		require.NoError(t, err)
		assert.Empty(t, target.uploadedSarifs)
		assert.Contains(t, buf.String(), "dry-run")
	})

	t.Run("polls until processing completes", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 1, Ref: "refs/heads/main", CommitSha: "sha1"},
			},
			sarifReports: map[int]string{
				1: `{"sarif":"report"}`,
			},
		}
		target := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{},
			uploadID: "upload-1",
			processingStatuses: []*github.SarifProcessingStatus{
				{Status: "pending"},
				{Status: "pending"},
				{Status: "complete"},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAnalyses(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		assert.Contains(t, buf.String(), "SARIF processing is still pending")
	})

	t.Run("returns error when processing fails", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 1, Ref: "refs/heads/main", CommitSha: "sha1"},
			},
			sarifReports: map[int]string{
				1: `{"sarif":"report"}`,
			},
		}
		target := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{},
			uploadID: "upload-1",
			processingStatuses: []*github.SarifProcessingStatus{
				{Status: "failed", Errors: []string{"invalid sarif"}},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAnalyses(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "SARIF processing failed")
		assert.Contains(t, err.Error(), "invalid sarif")
	})

	t.Run("skips analysis when SARIF report not found", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{
				{ID: 1, Ref: "refs/heads/main", CommitSha: "sha1"},
			},
			sarifReportErr: fmt.Errorf("not found"),
		}
		target := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAnalyses(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		assert.Contains(t, buf.String(), "Skipping analysis")
	})
}

// ---------------------------------------------------------------------------
// MigrateAlerts tests
// ---------------------------------------------------------------------------

func TestCodeScanningService_MigrateAlerts(t *testing.T) {
	t.Run("matches by most recent instance and updates state", func(t *testing.T) {
		inst := newInst("refs/heads/main", "sha1", "src/app.js", 10, 10, 1, 50)

		source := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 1, URL: "http://src/1", State: "dismissed", DismissedReason: "won't fix", DismissedComment: "Not applicable",
					RuleId: "js/xss", MostRecentInstance: inst,
				},
			},
		}
		target := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 10, URL: "http://tgt/10", State: "open",
					RuleId: "js/xss", MostRecentInstance: inst,
				},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		require.Len(t, target.alertUpdates, 1)
		assert.Equal(t, 10, target.alertUpdates[0].AlertNumber)
		assert.Equal(t, "dismissed", target.alertUpdates[0].State)
		assert.Equal(t, "won't fix", target.alertUpdates[0].DismissedReason)
	})

	t.Run("falls back to all source instances when most recent does not match", func(t *testing.T) {
		srcMostRecent := newInst("refs/heads/main", "sha-new", "src/app.js", 20, 20, 1, 50)
		srcOldInstance := github.CodeScanningAlertInstance{
			Ref: "refs/heads/main", CommitSha: "sha-old", Path: "src/app.js",
			StartLine: 10, EndLine: 10, StartColumn: 1, EndColumn: 50,
		}
		tgtMostRecent := newInst("refs/heads/main", "sha-old", "src/app.js", 10, 10, 1, 50)

		source := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 1, URL: "http://src/1", State: "dismissed", DismissedReason: "false positive",
					RuleId: "js/xss", MostRecentInstance: srcMostRecent,
				},
			},
			instances: map[int][]github.CodeScanningAlertInstance{
				1: {srcOldInstance},
			},
		}
		target := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 10, URL: "http://tgt/10", State: "open",
					RuleId: "js/xss", MostRecentInstance: tgtMostRecent,
				},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		require.Len(t, target.alertUpdates, 1)
		assert.Equal(t, 10, target.alertUpdates[0].AlertNumber)
	})

	t.Run("skips non-migratable states", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{Number: 1, URL: "http://src/1", State: "fixed", RuleId: "js/xss"},
			},
		}
		target := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		assert.Empty(t, target.alertUpdates)
		assert.Contains(t, buf.String(), "not migratable")
	})

	t.Run("skips already matching state", func(t *testing.T) {
		inst := newInst("refs/heads/main", "sha1", "src/app.js", 10, 10, 1, 50)

		source := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 1, URL: "http://src/1", State: "open",
					RuleId: "js/xss", MostRecentInstance: inst,
				},
			},
		}
		target := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 10, URL: "http://tgt/10", State: "open",
					RuleId: "js/xss", MostRecentInstance: inst,
				},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.NoError(t, err)
		assert.Empty(t, target.alertUpdates)
		assert.Contains(t, buf.String(), "already has the same state")
	})

	t.Run("dry run does not fetch target or update", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 1, URL: "http://src/1", State: "dismissed",
					RuleId:             "js/xss",
					MostRecentInstance: newInst("refs/heads/main", "sha1", "app.js", 1, 1, 1, 10),
				},
			},
		}
		target := &mockCodeScanningAPI{
			alertsErr: fmt.Errorf("should not be called in dry run"),
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", true)

		require.NoError(t, err)
		assert.Empty(t, target.alertUpdates)
		assert.Contains(t, buf.String(), "dry-run")
	})

	t.Run("returns error when no matching target found", func(t *testing.T) {
		source := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{
				{
					Number: 1, URL: "http://src/1", State: "dismissed",
					RuleId:             "js/xss",
					MostRecentInstance: newInst("refs/heads/main", "sha1", "app.js", 1, 1, 1, 10),
				},
			},
			instances: map[int][]github.CodeScanningAlertInstance{
				1: {},
			},
		}
		target := &mockCodeScanningAPI{
			alerts: []github.CodeScanningAlert{}, // no matching target
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", "main", false)

		require.Error(t, err)
		assert.Contains(t, err.Error(), "migration of code scanning alerts failed")
	})
}

// ---------------------------------------------------------------------------
// MigrateCodeScanningAlerts (full pipeline) test
// ---------------------------------------------------------------------------

func TestCodeScanningService_MigrateCodeScanningAlerts(t *testing.T) {
	t.Run("orchestrates analyses and alerts migration", func(t *testing.T) {
		inst := newInst("refs/heads/main", "sha1", "app.js", 10, 10, 1, 50)

		source := &mockCodeScanningAPI{
			defaultBranch: "main",
			analyses:      []github.CodeScanningAnalysis{},
			alerts: []github.CodeScanningAlert{
				{
					Number: 1, URL: "http://src/1", State: "dismissed", DismissedReason: "won't fix",
					RuleId: "js/xss", MostRecentInstance: inst,
				},
			},
		}
		target := &mockCodeScanningAPI{
			analyses: []github.CodeScanningAnalysis{},
			alerts: []github.CodeScanningAlert{
				{
					Number: 10, URL: "http://tgt/10", State: "open",
					RuleId: "js/xss", MostRecentInstance: inst,
				},
			},
		}

		var buf bytes.Buffer
		log := logger.New(false, &buf)
		svc := alerts.NewCodeScanningService(source, target, log, alerts.WithInitialDelay(0), alerts.WithPollDelay(0))
		err := svc.MigrateCodeScanningAlerts(context.Background(), "src-org", "src-repo", "tgt-org", "tgt-repo", false)

		require.NoError(t, err)
		require.Len(t, target.alertUpdates, 1)
		assert.Equal(t, 10, target.alertUpdates[0].AlertNumber)
	})
}
