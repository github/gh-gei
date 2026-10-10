package ado

import (
	"context"
	"io"
	"strings"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

const testUserID = "uid"

// mockInspectorAPI implements inspectorAPI with function fields for testing.
type mockInspectorAPI struct {
	getUserId           func(ctx context.Context) (string, error)
	getOrganizations    func(ctx context.Context, userId string) ([]string, error)
	getTeamProjects     func(ctx context.Context, org string) ([]string, error)
	getEnabledRepos     func(ctx context.Context, org, teamProject string) ([]Repository, error)
	populateRepoIdCache func(ctx context.Context, org, teamProject string) error
	getRepoId           func(ctx context.Context, org, teamProject, repo string) (string, error)
	getPipelines        func(ctx context.Context, org, teamProject, repoId string) ([]string, error)
	getPullRequestCount func(ctx context.Context, org, teamProject, repo string) (int, error)
}

func (m *mockInspectorAPI) GetUserId(ctx context.Context) (string, error) {
	return m.getUserId(ctx)
}

func (m *mockInspectorAPI) GetOrganizations(ctx context.Context, userId string) ([]string, error) {
	return m.getOrganizations(ctx, userId)
}

func (m *mockInspectorAPI) GetTeamProjects(ctx context.Context, org string) ([]string, error) {
	return m.getTeamProjects(ctx, org)
}

func (m *mockInspectorAPI) GetEnabledRepos(ctx context.Context, org, teamProject string) ([]Repository, error) {
	return m.getEnabledRepos(ctx, org, teamProject)
}

func (m *mockInspectorAPI) PopulateRepoIdCache(ctx context.Context, org, teamProject string) error {
	return m.populateRepoIdCache(ctx, org, teamProject)
}

func (m *mockInspectorAPI) GetRepoId(ctx context.Context, org, teamProject, repo string) (string, error) {
	return m.getRepoId(ctx, org, teamProject, repo)
}

func (m *mockInspectorAPI) GetPipelines(ctx context.Context, org, teamProject, repoId string) ([]string, error) {
	return m.getPipelines(ctx, org, teamProject, repoId)
}

func (m *mockInspectorAPI) GetPullRequestCount(ctx context.Context, org, teamProject, repo string) (int, error) {
	return m.getPullRequestCount(ctx, org, teamProject, repo)
}

const (
	adoOrg         = "ADO_ORG"
	adoTeamProject = "ADO_TEAM_PROJECT"
	fooRepo        = "FOO_REPO"
)

func newTestInspector(t *testing.T, api *mockInspectorAPI) *Inspector {
	t.Helper()
	log := logger.New(false)
	return NewInspector(log, api)
}

// ---------- GetOrgs ----------

func TestGetOrgs_ReturnsAllOrgs(t *testing.T) {
	userId := "user-123"
	orgs := []string{"my-org", "other-org"}

	api := &mockInspectorAPI{
		getUserId:        func(ctx context.Context) (string, error) { return userId, nil },
		getOrganizations: func(ctx context.Context, uid string) ([]string, error) { return orgs, nil },
	}
	ins := newTestInspector(t, api)

	result, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, orgs, result)
}

func TestGetOrgs_ReturnsSingleOrgWhenFilterSet(t *testing.T) {
	apiCalled := false
	api := &mockInspectorAPI{
		getUserId:        func(ctx context.Context) (string, error) { apiCalled = true; return "", nil },
		getOrganizations: func(ctx context.Context, uid string) ([]string, error) { apiCalled = true; return nil, nil },
	}
	ins := newTestInspector(t, api)
	ins.OrgFilter = adoOrg

	result, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, []string{adoOrg}, result)
	assert.False(t, apiCalled, "API should not be called when OrgFilter is set")
}

func TestGetOrgs_CachesResult(t *testing.T) {
	callCount := 0
	api := &mockInspectorAPI{
		getUserId: func(ctx context.Context) (string, error) {
			callCount++
			return testUserID, nil
		},
		getOrganizations: func(ctx context.Context, uid string) ([]string, error) {
			return []string{"org1"}, nil
		},
	}
	ins := newTestInspector(t, api)

	_, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	_, err = ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, 1, callCount, "GetUserId should only be called once due to caching")
}

// ---------- GetTeamProjects ----------

func TestGetTeamProjects_ReturnsAll(t *testing.T) {
	tps := []string{"foo", "bar"}
	api := &mockInspectorAPI{
		getTeamProjects: func(ctx context.Context, org string) ([]string, error) { return tps, nil },
	}
	ins := newTestInspector(t, api)

	result, err := ins.GetTeamProjects(context.Background(), adoOrg)
	require.NoError(t, err)
	assert.Equal(t, tps, result)
}

func TestGetTeamProjects_ReturnsSingleWhenFilterSet(t *testing.T) {
	apiCalled := false
	api := &mockInspectorAPI{
		getTeamProjects: func(ctx context.Context, org string) ([]string, error) {
			apiCalled = true
			return nil, nil
		},
	}
	ins := newTestInspector(t, api)
	ins.TeamProjectFilter = adoTeamProject

	result, err := ins.GetTeamProjects(context.Background(), adoOrg)
	require.NoError(t, err)
	assert.Equal(t, []string{adoTeamProject}, result)
	assert.False(t, apiCalled, "API should not be called when TeamProjectFilter is set")
}

func TestGetTeamProjects_CachesResult(t *testing.T) {
	callCount := 0
	api := &mockInspectorAPI{
		getTeamProjects: func(ctx context.Context, org string) ([]string, error) {
			callCount++
			return []string{"tp1"}, nil
		},
	}
	ins := newTestInspector(t, api)

	_, err := ins.GetTeamProjects(context.Background(), adoOrg)
	require.NoError(t, err)
	_, err = ins.GetTeamProjects(context.Background(), adoOrg)
	require.NoError(t, err)
	assert.Equal(t, 1, callCount, "API should only be called once due to caching")
}

// ---------- GetRepos ----------

func TestGetRepos_ReturnsAll(t *testing.T) {
	repos := []Repository{{Name: "foo"}, {Name: "bar"}}
	api := &mockInspectorAPI{
		getEnabledRepos: func(ctx context.Context, org, tp string) ([]Repository, error) { return repos, nil },
	}
	ins := newTestInspector(t, api)

	result, err := ins.GetRepos(context.Background(), adoOrg, adoTeamProject)
	require.NoError(t, err)
	assert.Equal(t, repos, result)
}

func TestGetRepos_CachesResult(t *testing.T) {
	callCount := 0
	api := &mockInspectorAPI{
		getEnabledRepos: func(ctx context.Context, org, tp string) ([]Repository, error) {
			callCount++
			return []Repository{{Name: "r1"}}, nil
		},
	}
	ins := newTestInspector(t, api)

	_, err := ins.GetRepos(context.Background(), adoOrg, adoTeamProject)
	require.NoError(t, err)
	_, err = ins.GetRepos(context.Background(), adoOrg, adoTeamProject)
	require.NoError(t, err)
	assert.Equal(t, 1, callCount)
}

// ---------- GetPipelines ----------

func TestGetPipelines_ReturnsAll(t *testing.T) {
	repoId := "repo-id-123"
	pipelines := []string{"foo", "bar"}

	api := &mockInspectorAPI{
		populateRepoIdCache: func(ctx context.Context, org, tp string) error { return nil },
		getRepoId:           func(ctx context.Context, org, tp, repo string) (string, error) { return repoId, nil },
		getPipelines: func(ctx context.Context, org, tp, rid string) ([]string, error) {
			assert.Equal(t, repoId, rid)
			return pipelines, nil
		},
	}
	ins := newTestInspector(t, api)

	result, err := ins.GetPipelines(context.Background(), adoOrg, adoTeamProject, fooRepo)
	require.NoError(t, err)
	assert.Equal(t, pipelines, result)
}

func TestGetPipelines_CachesResult(t *testing.T) {
	callCount := 0
	api := &mockInspectorAPI{
		populateRepoIdCache: func(ctx context.Context, org, tp string) error { return nil },
		getRepoId:           func(ctx context.Context, org, tp, repo string) (string, error) { return "rid", nil },
		getPipelines: func(ctx context.Context, org, tp, rid string) ([]string, error) {
			callCount++
			return []string{"p1"}, nil
		},
	}
	ins := newTestInspector(t, api)

	_, err := ins.GetPipelines(context.Background(), adoOrg, adoTeamProject, fooRepo)
	require.NoError(t, err)
	_, err = ins.GetPipelines(context.Background(), adoOrg, adoTeamProject, fooRepo)
	require.NoError(t, err)
	assert.Equal(t, 1, callCount)
}

// ---------- GetPullRequestCount ----------

func TestGetPullRequestCount_ReturnsCount(t *testing.T) {
	api := &mockInspectorAPI{
		getPullRequestCount: func(ctx context.Context, org, tp, repo string) (int, error) { return 42, nil },
	}
	ins := newTestInspector(t, api)

	count, err := ins.GetPullRequestCount(context.Background(), adoOrg, adoTeamProject, fooRepo)
	require.NoError(t, err)
	assert.Equal(t, 42, count)
}

func TestGetPullRequestCount_CachesResult(t *testing.T) {
	callCount := 0
	api := &mockInspectorAPI{
		getPullRequestCount: func(ctx context.Context, org, tp, repo string) (int, error) {
			callCount++
			return 7, nil
		},
	}
	ins := newTestInspector(t, api)

	_, err := ins.GetPullRequestCount(context.Background(), adoOrg, adoTeamProject, fooRepo)
	require.NoError(t, err)
	_, err = ins.GetPullRequestCount(context.Background(), adoOrg, adoTeamProject, fooRepo)
	require.NoError(t, err)
	assert.Equal(t, 1, callCount)
}

// ---------- LoadReposCsv ----------

func csvStream(content string) func(string) (io.ReadCloser, error) {
	return func(_ string) (io.ReadCloser, error) {
		return io.NopCloser(strings.NewReader(content)), nil
	}
}

func TestLoadReposCsv_SetsOrgs(t *testing.T) {
	ins := newTestInspector(t, &mockInspectorAPI{})
	ins.OpenFileStream = csvStream("org,teamproject,repo\nADO_ORG,ADO_TEAM_PROJECT,FOO_REPO\n")

	err := ins.LoadReposCsv("repos.csv")
	require.NoError(t, err)

	orgs, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, []string{adoOrg}, orgs)
}

func TestLoadReposCsv_SetsTeamProjects(t *testing.T) {
	ins := newTestInspector(t, &mockInspectorAPI{})
	ins.OpenFileStream = csvStream("org,teamproject,repo\nADO_ORG,ADO_TEAM_PROJECT,FOO_REPO\n")

	err := ins.LoadReposCsv("repos.csv")
	require.NoError(t, err)

	tps, err := ins.GetTeamProjects(context.Background(), adoOrg)
	require.NoError(t, err)
	assert.Equal(t, []string{adoTeamProject}, tps)
}

func TestLoadReposCsv_SetsRepos(t *testing.T) {
	ins := newTestInspector(t, &mockInspectorAPI{})
	ins.OpenFileStream = csvStream("org,teamproject,repo\nADO_ORG,ADO_TEAM_PROJECT,FOO_REPO\n")

	err := ins.LoadReposCsv("repos.csv")
	require.NoError(t, err)

	repos, err := ins.GetRepos(context.Background(), adoOrg, adoTeamProject)
	require.NoError(t, err)
	require.Len(t, repos, 1)
	assert.Equal(t, fooRepo, repos[0].Name)
}

func TestLoadReposCsv_DeduplicatesOrgs(t *testing.T) {
	ins := newTestInspector(t, &mockInspectorAPI{})
	ins.OpenFileStream = csvStream("org,teamproject,repo\nORG1,TP1,R1\nORG1,TP1,R2\nORG1,TP2,R3\n")

	err := ins.LoadReposCsv("repos.csv")
	require.NoError(t, err)

	orgs, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, []string{"ORG1"}, orgs)
}

func TestLoadReposCsv_MultipleOrgs(t *testing.T) {
	ins := newTestInspector(t, &mockInspectorAPI{})
	ins.OpenFileStream = csvStream("org,teamproject,repo\nORG1,TP1,R1\nORG2,TP2,R2\n")

	err := ins.LoadReposCsv("repos.csv")
	require.NoError(t, err)

	orgs, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, []string{"ORG1", "ORG2"}, orgs)
}

func TestLoadReposCsv_QuotedFields(t *testing.T) {
	// Go's csv reader handles quoted fields natively.
	ins := newTestInspector(t, &mockInspectorAPI{})
	ins.OpenFileStream = csvStream("org,teamproject,repo\n\"ADO_ORG\",\"ADO_TEAM_PROJECT\",\"FOO_REPO\"\n")

	err := ins.LoadReposCsv("repos.csv")
	require.NoError(t, err)

	orgs, err := ins.GetOrgs(context.Background())
	require.NoError(t, err)
	assert.Equal(t, []string{adoOrg}, orgs)
}

// ---------- Count aggregations ----------

func TestGetRepoCount(t *testing.T) {
	api := &mockInspectorAPI{
		getUserId:        func(ctx context.Context) (string, error) { return testUserID, nil },
		getOrganizations: func(ctx context.Context, uid string) ([]string, error) { return []string{"org1"}, nil },
		getTeamProjects:  func(ctx context.Context, org string) ([]string, error) { return []string{"tp1", "tp2"}, nil },
		getEnabledRepos: func(ctx context.Context, org, tp string) ([]Repository, error) {
			return []Repository{{Name: "r1"}, {Name: "r2"}}, nil
		},
	}
	ins := newTestInspector(t, api)

	count, err := ins.GetRepoCount(context.Background())
	require.NoError(t, err)
	assert.Equal(t, 4, count) // 2 repos × 2 team projects
}

func TestGetPipelineCount(t *testing.T) {
	api := &mockInspectorAPI{
		getUserId:        func(ctx context.Context) (string, error) { return testUserID, nil },
		getOrganizations: func(ctx context.Context, uid string) ([]string, error) { return []string{"org1"}, nil },
		getTeamProjects:  func(ctx context.Context, org string) ([]string, error) { return []string{"tp1"}, nil },
		getEnabledRepos: func(ctx context.Context, org, tp string) ([]Repository, error) {
			return []Repository{{Name: "r1"}}, nil
		},
		populateRepoIdCache: func(ctx context.Context, org, tp string) error { return nil },
		getRepoId:           func(ctx context.Context, org, tp, repo string) (string, error) { return "rid", nil },
		getPipelines: func(ctx context.Context, org, tp, rid string) ([]string, error) {
			return []string{"p1", "p2", "p3"}, nil
		},
	}
	ins := newTestInspector(t, api)

	count, err := ins.GetPipelineCount(context.Background())
	require.NoError(t, err)
	assert.Equal(t, 3, count)
}

// ---------- ReplaceInvalidCharactersWithDash ----------

func TestReplaceInvalidCharactersWithDash(t *testing.T) {
	tests := []struct {
		name  string
		input string
		want  string
	}{
		{"no change for valid", "hello-world.v2", "hello-world.v2"},
		{"spaces to dash", "hello world", "hello-world"},
		{"multiple spaces to single dash", "hello   world", "hello-world"},
		{"special chars to dash", "hello@world!", "hello-world-"},
		{"underscores preserved", "hello_world", "hello_world"},
		{"dots preserved", "v1.2.3", "v1.2.3"},
		{"dashes preserved", "my-project", "my-project"},
		{"mixed special chars", "org/team project (test)", "org-team-project-test-"},
		{"empty string", "", ""},
		{"consecutive specials", "a$$b%%c", "a-b-c"},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			assert.Equal(t, tt.want, ReplaceInvalidCharactersWithDash(tt.input))
		})
	}
}

// ---------- OutputRepoListToLog ----------

func TestOutputRepoListToLog(t *testing.T) {
	var buf strings.Builder
	log := logger.New(false, &buf)
	ins := NewInspector(log, &mockInspectorAPI{})

	// Populate cache directly.
	ins.repos["org1"] = map[string][]Repository{
		"tp1": {{Name: "repo-a"}, {Name: "repo-b"}},
	}

	ins.OutputRepoListToLog()

	output := buf.String()
	assert.Contains(t, output, "org1")
	assert.Contains(t, output, "tp1")
	assert.Contains(t, output, "repo-a")
	assert.Contains(t, output, "repo-b")
}
