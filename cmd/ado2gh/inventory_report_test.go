package main

import (
	"bytes"
	"context"
	"testing"
	"time"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockInventoryInspector struct {
	orgFilter string

	getOrgsFunc                           func(ctx context.Context) ([]string, error)
	getTeamProjectsFunc                   func(ctx context.Context, org string) ([]string, error)
	getReposFunc                          func(ctx context.Context, org, tp string) ([]ado.Repository, error)
	getPipelinesFunc                      func(ctx context.Context, org, tp, repo string) ([]string, error)
	getTeamProjectCountForOrgFunc         func(ctx context.Context, org string) (int, error)
	getRepoCountForOrgFunc                func(ctx context.Context, org string) (int, error)
	getPipelineCountForOrgFunc            func(ctx context.Context, org string) (int, error)
	getPullRequestCountForOrgFunc         func(ctx context.Context, org string) (int, error)
	getPipelineCountForTeamProjectFunc    func(ctx context.Context, org, tp string) (int, error)
	getPullRequestCountForTeamProjectFunc func(ctx context.Context, org, tp string) (int, error)
	getPullRequestCountFunc               func(ctx context.Context, org, tp, repo string) (int, error)
	getTeamProjectCountFunc               func(ctx context.Context) (int, error)
	getRepoCountFunc                      func(ctx context.Context) (int, error)
	getPipelineCountFunc                  func(ctx context.Context) (int, error)
}

func (m *mockInventoryInspector) SetOrgFilter(f string) { m.orgFilter = f }
func (m *mockInventoryInspector) GetOrgFilter() string  { return m.orgFilter }
func (m *mockInventoryInspector) GetOrgs(ctx context.Context) ([]string, error) {
	return m.getOrgsFunc(ctx)
}

func (m *mockInventoryInspector) GetTeamProjects(ctx context.Context, org string) ([]string, error) {
	return m.getTeamProjectsFunc(ctx, org)
}

func (m *mockInventoryInspector) GetRepos(ctx context.Context, org, tp string) ([]ado.Repository, error) {
	return m.getReposFunc(ctx, org, tp)
}

func (m *mockInventoryInspector) GetPipelines(ctx context.Context, org, tp, repo string) ([]string, error) {
	return m.getPipelinesFunc(ctx, org, tp, repo)
}

func (m *mockInventoryInspector) GetTeamProjectCountForOrg(ctx context.Context, org string) (int, error) {
	return m.getTeamProjectCountForOrgFunc(ctx, org)
}

func (m *mockInventoryInspector) GetRepoCountForOrg(ctx context.Context, org string) (int, error) {
	return m.getRepoCountForOrgFunc(ctx, org)
}

func (m *mockInventoryInspector) GetPipelineCountForOrg(ctx context.Context, org string) (int, error) {
	return m.getPipelineCountForOrgFunc(ctx, org)
}

func (m *mockInventoryInspector) GetPullRequestCountForOrg(ctx context.Context, org string) (int, error) {
	return m.getPullRequestCountForOrgFunc(ctx, org)
}

func (m *mockInventoryInspector) GetPipelineCountForTeamProject(ctx context.Context, org, tp string) (int, error) {
	return m.getPipelineCountForTeamProjectFunc(ctx, org, tp)
}

func (m *mockInventoryInspector) GetPullRequestCountForTeamProject(ctx context.Context, org, tp string) (int, error) {
	return m.getPullRequestCountForTeamProjectFunc(ctx, org, tp)
}

func (m *mockInventoryInspector) GetPullRequestCount(ctx context.Context, org, tp, repo string) (int, error) {
	return m.getPullRequestCountFunc(ctx, org, tp, repo)
}

func (m *mockInventoryInspector) GetTeamProjectCount(ctx context.Context) (int, error) {
	return m.getTeamProjectCountFunc(ctx)
}

func (m *mockInventoryInspector) GetRepoCount(ctx context.Context) (int, error) {
	return m.getRepoCountFunc(ctx)
}

func (m *mockInventoryInspector) GetPipelineCount(ctx context.Context) (int, error) {
	return m.getPipelineCountFunc(ctx)
}

type mockInventoryAPI struct {
	getOrgOwnerFunc         func(ctx context.Context, org string) (string, error)
	isCallerOrgAdminFunc    func(ctx context.Context, org string) (bool, error)
	getLastPushDateFunc     func(ctx context.Context, org, tp, repo string) (time.Time, error)
	getPushersSinceFunc     func(ctx context.Context, org, tp, repo string, fromDate time.Time) ([]string, error)
	getCommitCountSinceFunc func(ctx context.Context, org, tp, repo string, fromDate time.Time) (int, error)
	getPipelineIdFunc       func(ctx context.Context, org, tp, pipeline string) (int, error)
}

func (m *mockInventoryAPI) GetOrgOwner(ctx context.Context, org string) (string, error) {
	return m.getOrgOwnerFunc(ctx, org)
}

func (m *mockInventoryAPI) IsCallerOrgAdmin(ctx context.Context, org string) (bool, error) {
	return m.isCallerOrgAdminFunc(ctx, org)
}

func (m *mockInventoryAPI) GetLastPushDate(ctx context.Context, org, tp, repo string) (time.Time, error) {
	return m.getLastPushDateFunc(ctx, org, tp, repo)
}

func (m *mockInventoryAPI) GetPushersSince(ctx context.Context, org, tp, repo string, fromDate time.Time) ([]string, error) {
	return m.getPushersSinceFunc(ctx, org, tp, repo, fromDate)
}

func (m *mockInventoryAPI) GetCommitCountSince(ctx context.Context, org, tp, repo string, fromDate time.Time) (int, error) {
	return m.getCommitCountSinceFunc(ctx, org, tp, repo, fromDate)
}

func (m *mockInventoryAPI) GetPipelineId(ctx context.Context, org, tp, pipeline string) (int, error) {
	return m.getPipelineIdFunc(ctx, org, tp, pipeline)
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func defaultMockInspector() *mockInventoryInspector {
	lastPush := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)
	_ = lastPush

	return &mockInventoryInspector{
		getOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		getTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"my-tp"}, nil
		},
		getReposFunc: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{{Name: "my-repo", Size: 1000}}, nil
		},
		getPipelinesFunc: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"my-pipeline"}, nil
		},
		getTeamProjectCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 1, nil
		},
		getRepoCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 1, nil
		},
		getPipelineCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 1, nil
		},
		getPullRequestCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 5, nil
		},
		getPipelineCountForTeamProjectFunc: func(_ context.Context, _, _ string) (int, error) {
			return 1, nil
		},
		getPullRequestCountForTeamProjectFunc: func(_ context.Context, _, _ string) (int, error) {
			return 5, nil
		},
		getPullRequestCountFunc: func(_ context.Context, _, _, _ string) (int, error) {
			return 5, nil
		},
		getTeamProjectCountFunc: func(_ context.Context) (int, error) {
			return 1, nil
		},
		getRepoCountFunc: func(_ context.Context) (int, error) {
			return 1, nil
		},
		getPipelineCountFunc: func(_ context.Context) (int, error) {
			return 1, nil
		},
	}
}

func defaultMockAPI() *mockInventoryAPI {
	return &mockInventoryAPI{
		getOrgOwnerFunc: func(_ context.Context, _ string) (string, error) {
			return "owner@example.com", nil
		},
		isCallerOrgAdminFunc: func(_ context.Context, _ string) (bool, error) {
			return true, nil
		},
		getLastPushDateFunc: func(_ context.Context, _, _, _ string) (time.Time, error) {
			return time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC), nil
		},
		getPushersSinceFunc: func(_ context.Context, _, _, _ string, _ time.Time) ([]string, error) {
			return []string{"alice"}, nil
		},
		getCommitCountSinceFunc: func(_ context.Context, _, _, _ string, _ time.Time) (int, error) {
			return 10, nil
		},
		getPipelineIdFunc: func(_ context.Context, _, _, _ string) (int, error) {
			return 42, nil
		},
	}
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestInventoryReport_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	ins := defaultMockInspector()
	api := defaultMockAPI()

	writtenFiles := make(map[string]string)
	writeFile := func(path, content string) error {
		writtenFiles[path] = content
		return nil
	}

	cmd := newInventoryReportCmd(ins, api, log, writeFile)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Creating inventory report...")

	// All 4 CSV files should have been written
	assert.Contains(t, writtenFiles, "orgs.csv")
	assert.Contains(t, writtenFiles, "team-projects.csv")
	assert.Contains(t, writtenFiles, "repos.csv")
	assert.Contains(t, writtenFiles, "pipelines.csv")

	// Verify orgs.csv has correct header
	assert.Contains(t, writtenFiles["orgs.csv"], "name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin,pr-count")
}

func TestInventoryReport_ScopedToOrg(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	ins := defaultMockInspector()
	api := defaultMockAPI()

	writtenFiles := make(map[string]string)
	writeFile := func(path, content string) error {
		writtenFiles[path] = content
		return nil
	}

	cmd := newInventoryReportCmd(ins, api, log, writeFile)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--ado-org", "specific-org"})

	err := cmd.Execute()
	require.NoError(t, err)

	// Verify org filter was set
	assert.Equal(t, "specific-org", ins.orgFilter)
}

func TestInventoryReport_Minimal(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	ins := defaultMockInspector()
	api := defaultMockAPI()

	writtenFiles := make(map[string]string)
	writeFile := func(path, content string) error {
		writtenFiles[path] = content
		return nil
	}

	cmd := newInventoryReportCmd(ins, api, log, writeFile)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--minimal"})

	err := cmd.Execute()
	require.NoError(t, err)

	// Minimal orgs CSV should NOT have pr-count column
	assert.Contains(t, writtenFiles["orgs.csv"], "name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin\n")
	assert.NotContains(t, writtenFiles["orgs.csv"], "pr-count")

	// Minimal repos CSV should NOT have most-active-contributor, pr-count, commits-past-year
	assert.NotContains(t, writtenFiles["repos.csv"], "most-active-contributor")
}
