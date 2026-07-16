package ado

import (
	"context"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockCSVInspector struct {
	GetOrgsFunc                           func(ctx context.Context) ([]string, error)
	GetTeamProjectsFunc                   func(ctx context.Context, org string) ([]string, error)
	GetReposFunc                          func(ctx context.Context, org, tp string) ([]Repository, error)
	GetPipelinesFunc                      func(ctx context.Context, org, tp, repo string) ([]string, error)
	GetTeamProjectCountForOrgFunc         func(ctx context.Context, org string) (int, error)
	GetRepoCountForOrgFunc                func(ctx context.Context, org string) (int, error)
	GetPipelineCountForOrgFunc            func(ctx context.Context, org string) (int, error)
	GetPullRequestCountForOrgFunc         func(ctx context.Context, org string) (int, error)
	GetPipelineCountForTeamProjectFunc    func(ctx context.Context, org, tp string) (int, error)
	GetPullRequestCountForTeamProjectFunc func(ctx context.Context, org, tp string) (int, error)
	GetPullRequestCountFunc               func(ctx context.Context, org, tp, repo string) (int, error)
}

func (m *mockCSVInspector) GetOrgs(ctx context.Context) ([]string, error) {
	return m.GetOrgsFunc(ctx)
}

func (m *mockCSVInspector) GetTeamProjects(ctx context.Context, org string) ([]string, error) {
	return m.GetTeamProjectsFunc(ctx, org)
}

func (m *mockCSVInspector) GetRepos(ctx context.Context, org, tp string) ([]Repository, error) {
	return m.GetReposFunc(ctx, org, tp)
}

func (m *mockCSVInspector) GetPipelines(ctx context.Context, org, tp, repo string) ([]string, error) {
	return m.GetPipelinesFunc(ctx, org, tp, repo)
}

func (m *mockCSVInspector) GetTeamProjectCountForOrg(ctx context.Context, org string) (int, error) {
	return m.GetTeamProjectCountForOrgFunc(ctx, org)
}

func (m *mockCSVInspector) GetRepoCountForOrg(ctx context.Context, org string) (int, error) {
	return m.GetRepoCountForOrgFunc(ctx, org)
}

func (m *mockCSVInspector) GetPipelineCountForOrg(ctx context.Context, org string) (int, error) {
	return m.GetPipelineCountForOrgFunc(ctx, org)
}

func (m *mockCSVInspector) GetPullRequestCountForOrg(ctx context.Context, org string) (int, error) {
	return m.GetPullRequestCountForOrgFunc(ctx, org)
}

func (m *mockCSVInspector) GetPipelineCountForTeamProject(ctx context.Context, org, tp string) (int, error) {
	return m.GetPipelineCountForTeamProjectFunc(ctx, org, tp)
}

func (m *mockCSVInspector) GetPullRequestCountForTeamProject(ctx context.Context, org, tp string) (int, error) {
	return m.GetPullRequestCountForTeamProjectFunc(ctx, org, tp)
}

func (m *mockCSVInspector) GetPullRequestCount(ctx context.Context, org, tp, repo string) (int, error) {
	return m.GetPullRequestCountFunc(ctx, org, tp, repo)
}

type mockCSVAdoAPI struct {
	GetOrgOwnerFunc         func(ctx context.Context, org string) (string, error)
	IsCallerOrgAdminFunc    func(ctx context.Context, org string) (bool, error)
	GetLastPushDateFunc     func(ctx context.Context, org, tp, repo string) (time.Time, error)
	GetPushersSinceFunc     func(ctx context.Context, org, tp, repo string, fromDate time.Time) ([]string, error)
	GetCommitCountSinceFunc func(ctx context.Context, org, tp, repo string, fromDate time.Time) (int, error)
	GetPipelineIdFunc       func(ctx context.Context, org, tp, pipeline string) (int, error)
}

func (m *mockCSVAdoAPI) GetOrgOwner(ctx context.Context, org string) (string, error) {
	return m.GetOrgOwnerFunc(ctx, org)
}

func (m *mockCSVAdoAPI) IsCallerOrgAdmin(ctx context.Context, org string) (bool, error) {
	return m.IsCallerOrgAdminFunc(ctx, org)
}

func (m *mockCSVAdoAPI) GetLastPushDate(ctx context.Context, org, tp, repo string) (time.Time, error) {
	return m.GetLastPushDateFunc(ctx, org, tp, repo)
}

func (m *mockCSVAdoAPI) GetPushersSince(ctx context.Context, org, tp, repo string, fromDate time.Time) ([]string, error) {
	return m.GetPushersSinceFunc(ctx, org, tp, repo, fromDate)
}

func (m *mockCSVAdoAPI) GetCommitCountSince(ctx context.Context, org, tp, repo string, fromDate time.Time) (int, error) {
	return m.GetCommitCountSinceFunc(ctx, org, tp, repo, fromDate)
}

func (m *mockCSVAdoAPI) GetPipelineId(ctx context.Context, org, tp, pipeline string) (int, error) {
	return m.GetPipelineIdFunc(ctx, org, tp, pipeline)
}

// ---------------------------------------------------------------------------
// Helper function tests
// ---------------------------------------------------------------------------

func TestFormatWithThousandsSeparator(t *testing.T) {
	tests := []struct {
		input    uint64
		expected string
	}{
		{0, "0"},
		{1, "1"},
		{12, "12"},
		{123, "123"},
		{1234, "1,234"},
		{12345, "12,345"},
		{123456, "123,456"},
		{1234567, "1,234,567"},
		{1234567890, "1,234,567,890"},
	}

	for _, tt := range tests {
		t.Run(tt.expected, func(t *testing.T) {
			got := formatWithThousandsSeparator(tt.input)
			assert.Equal(t, tt.expected, got)
		})
	}
}

func TestGetMostActiveContributor_Basic(t *testing.T) {
	pushers := []string{"alice", "bob", "alice", "charlie", "alice", "bob"}
	got := getMostActiveContributor(pushers)
	assert.Equal(t, "alice", got)
}

func TestGetMostActiveContributor_AllService(t *testing.T) {
	pushers := []string{"Build Service", "Azure DevOps Service"}
	got := getMostActiveContributor(pushers)
	assert.Equal(t, "N/A", got)
}

func TestGetMostActiveContributor_Empty(t *testing.T) {
	got := getMostActiveContributor(nil)
	assert.Equal(t, "N/A", got)
}

func TestGetMostActiveContributor_MixedWithService(t *testing.T) {
	pushers := []string{"Build Service", "alice", "Build Service", "alice", "bob"}
	got := getMostActiveContributor(pushers)
	assert.Equal(t, "alice", got)
}

// ---------------------------------------------------------------------------
// GenerateOrgsCsv tests
// ---------------------------------------------------------------------------

func TestGenerateOrgsCsv_OneOrg(t *testing.T) {
	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 5, nil
		},
		GetRepoCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 10, nil
		},
		GetPipelineCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 3, nil
		},
		GetPullRequestCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 42, nil
		},
	}
	api := &mockCSVAdoAPI{
		GetOrgOwnerFunc: func(_ context.Context, _ string) (string, error) {
			return "owner@example.com", nil
		},
		IsCallerOrgAdminFunc: func(_ context.Context, _ string) (bool, error) {
			return true, nil
		},
	}

	csv, err := GenerateOrgsCsv(context.Background(), ins, api, false)
	require.NoError(t, err)

	expected := "name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin,pr-count\n" +
		"\"my-org\",\"https://dev.azure.com/my-org\",\"owner@example.com\",5,10,3,True,42\n"
	assert.Equal(t, expected, csv)
}

func TestGenerateOrgsCsv_Minimal(t *testing.T) {
	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 5, nil
		},
		GetRepoCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 10, nil
		},
		GetPipelineCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 3, nil
		},
	}
	api := &mockCSVAdoAPI{
		GetOrgOwnerFunc: func(_ context.Context, _ string) (string, error) {
			return "owner@example.com", nil
		},
		IsCallerOrgAdminFunc: func(_ context.Context, _ string) (bool, error) {
			return false, nil
		},
	}

	csv, err := GenerateOrgsCsv(context.Background(), ins, api, true)
	require.NoError(t, err)

	expected := "name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin\n" +
		"\"my-org\",\"https://dev.azure.com/my-org\",\"owner@example.com\",5,10,3,False\n"
	assert.Equal(t, expected, csv)
}

// ---------------------------------------------------------------------------
// GenerateTeamProjectsCsv tests
// ---------------------------------------------------------------------------

func TestGenerateTeamProjectsCsv_OneTeamProject(t *testing.T) {
	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"my-tp"}, nil
		},
		GetRepoCountForOrgFunc: func(_ context.Context, _ string) (int, error) {
			return 10, nil // unused for team project CSV — we need per-tp count
		},
		GetPipelineCountForTeamProjectFunc: func(_ context.Context, _, _ string) (int, error) {
			return 7, nil
		},
		GetPullRequestCountForTeamProjectFunc: func(_ context.Context, _, _ string) (int, error) {
			return 25, nil
		},
		GetReposFunc: func(_ context.Context, _, _ string) ([]Repository, error) {
			return make([]Repository, 4), nil // 4 repos
		},
	}
	api := &mockCSVAdoAPI{}

	csv, err := GenerateTeamProjectsCsv(context.Background(), ins, api, false)
	require.NoError(t, err)

	expected := "org,teamproject,url,repo-count,pipeline-count,pr-count\n" +
		"\"my-org\",\"my-tp\",\"https://dev.azure.com/my-org/my-tp\",4,7,25\n"
	assert.Equal(t, expected, csv)
}

func TestGenerateTeamProjectsCsv_Minimal(t *testing.T) {
	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"my-tp"}, nil
		},
		GetPipelineCountForTeamProjectFunc: func(_ context.Context, _, _ string) (int, error) {
			return 7, nil
		},
		GetReposFunc: func(_ context.Context, _, _ string) ([]Repository, error) {
			return make([]Repository, 4), nil
		},
	}
	api := &mockCSVAdoAPI{}

	csv, err := GenerateTeamProjectsCsv(context.Background(), ins, api, true)
	require.NoError(t, err)

	expected := "org,teamproject,url,repo-count,pipeline-count\n" +
		"\"my-org\",\"my-tp\",\"https://dev.azure.com/my-org/my-tp\",4,7\n"
	assert.Equal(t, expected, csv)
}

// ---------------------------------------------------------------------------
// GenerateReposCsv tests
// ---------------------------------------------------------------------------

func TestGenerateReposCsv_OneRepo(t *testing.T) {
	lastPush := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"my-tp"}, nil
		},
		GetReposFunc: func(_ context.Context, _, _ string) ([]Repository, error) {
			return []Repository{{Name: "my-repo", Size: 12345}}, nil
		},
		GetPipelinesFunc: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"pipe1", "pipe2"}, nil
		},
		GetPullRequestCountFunc: func(_ context.Context, _, _, _ string) (int, error) {
			return 17, nil
		},
	}
	api := &mockCSVAdoAPI{
		GetLastPushDateFunc: func(_ context.Context, _, _, _ string) (time.Time, error) {
			return lastPush, nil
		},
		GetPushersSinceFunc: func(_ context.Context, _, _, _ string, _ time.Time) ([]string, error) {
			return []string{"alice", "bob", "alice"}, nil
		},
		GetCommitCountSinceFunc: func(_ context.Context, _, _, _ string, _ time.Time) (int, error) {
			return 99, nil
		},
	}

	csv, err := GenerateReposCsv(context.Background(), ins, api, false)
	require.NoError(t, err)

	expected := "org,teamproject,repo,url,last-push-date,pipeline-count,compressed-repo-size-in-bytes,most-active-contributor,pr-count,commits-past-year\n" +
		"\"my-org\",\"my-tp\",\"my-repo\",\"https://dev.azure.com/my-org/my-tp/_git/my-repo\",\"15-Jun-2023 02:30 PM\",2,\"12,345\",\"alice\",17,99\n"
	assert.Equal(t, expected, csv)
}

func TestGenerateReposCsv_FilterServiceAccounts(t *testing.T) {
	lastPush := time.Date(2023, 1, 1, 0, 0, 0, 0, time.UTC)

	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"org"}, nil
		},
		GetTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"tp"}, nil
		},
		GetReposFunc: func(_ context.Context, _, _ string) ([]Repository, error) {
			return []Repository{{Name: "repo", Size: 0}}, nil
		},
		GetPipelinesFunc: func(_ context.Context, _, _, _ string) ([]string, error) {
			return nil, nil
		},
		GetPullRequestCountFunc: func(_ context.Context, _, _, _ string) (int, error) {
			return 0, nil
		},
	}
	api := &mockCSVAdoAPI{
		GetLastPushDateFunc: func(_ context.Context, _, _, _ string) (time.Time, error) {
			return lastPush, nil
		},
		GetPushersSinceFunc: func(_ context.Context, _, _, _ string, _ time.Time) ([]string, error) {
			return []string{"Build Service", "alice", "Azure DevOps Service", "alice", "bob"}, nil
		},
		GetCommitCountSinceFunc: func(_ context.Context, _, _, _ string, _ time.Time) (int, error) {
			return 5, nil
		},
	}

	csv, err := GenerateReposCsv(context.Background(), ins, api, false)
	require.NoError(t, err)

	// "alice" appears twice after filtering, "bob" once → most active = "alice"
	assert.Contains(t, csv, "\"alice\"")
	assert.NotContains(t, csv, "Build Service")
}

func TestGenerateReposCsv_Minimal(t *testing.T) {
	lastPush := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"my-tp"}, nil
		},
		GetReposFunc: func(_ context.Context, _, _ string) ([]Repository, error) {
			return []Repository{{Name: "my-repo", Size: 12345}}, nil
		},
		GetPipelinesFunc: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"pipe1"}, nil
		},
	}
	api := &mockCSVAdoAPI{
		GetLastPushDateFunc: func(_ context.Context, _, _, _ string) (time.Time, error) {
			return lastPush, nil
		},
	}

	csv, err := GenerateReposCsv(context.Background(), ins, api, true)
	require.NoError(t, err)

	expected := "org,teamproject,repo,url,last-push-date,pipeline-count,compressed-repo-size-in-bytes\n" +
		"\"my-org\",\"my-tp\",\"my-repo\",\"https://dev.azure.com/my-org/my-tp/_git/my-repo\",\"15-Jun-2023 02:30 PM\",1,\"12,345\"\n"
	assert.Equal(t, expected, csv)

	// Should NOT contain full-mode columns
	assert.NotContains(t, csv, "most-active-contributor")
	assert.NotContains(t, csv, "pr-count")
	assert.NotContains(t, csv, "commits-past-year")
}

// ---------------------------------------------------------------------------
// GeneratePipelinesCsv tests
// ---------------------------------------------------------------------------

func TestGeneratePipelinesCsv_OnePipeline(t *testing.T) {
	ins := &mockCSVInspector{
		GetOrgsFunc: func(_ context.Context) ([]string, error) {
			return []string{"my-org"}, nil
		},
		GetTeamProjectsFunc: func(_ context.Context, _ string) ([]string, error) {
			return []string{"my-tp"}, nil
		},
		GetReposFunc: func(_ context.Context, _, _ string) ([]Repository, error) {
			return []Repository{{Name: "my-repo"}}, nil
		},
		GetPipelinesFunc: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"my-pipeline"}, nil
		},
	}
	api := &mockCSVAdoAPI{
		GetPipelineIdFunc: func(_ context.Context, _, _, _ string) (int, error) {
			return 42, nil
		},
	}

	csv, err := GeneratePipelinesCsv(context.Background(), ins, api)
	require.NoError(t, err)

	expected := "org,teamproject,repo,pipeline,url\n" +
		"\"my-org\",\"my-tp\",\"my-repo\",\"my-pipeline\",\"https://dev.azure.com/my-org/my-tp/_build?definitionId=42\"\n"
	assert.Equal(t, expected, csv)
}
