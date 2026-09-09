package main

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"strings"
	"testing"
	"time"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementation
// ---------------------------------------------------------------------------

type mockInvReportBbsAPI struct {
	getProjectsFunc                     func(ctx context.Context) ([]invProject, error)
	getProjectFunc                      func(ctx context.Context, projectKey string) (invProject, error)
	getReposFunc                        func(ctx context.Context, projectKey string) ([]invRepository, error)
	getRepositoryPullRequestsFunc       func(ctx context.Context, projectKey, repo string) ([]invPullRequest, error)
	getRepositoryLatestCommitDateFunc   func(ctx context.Context, projectKey, repo string) (*time.Time, error)
	getRepositoryAndAttachmentsSizeFunc func(ctx context.Context, projectKey, repo string) (uint64, uint64, error)
	getIsRepositoryArchivedFunc         func(ctx context.Context, projectKey, repo string) (bool, error)
}

func (m *mockInvReportBbsAPI) GetProjects(ctx context.Context) ([]invProject, error) {
	return m.getProjectsFunc(ctx)
}

func (m *mockInvReportBbsAPI) GetProject(ctx context.Context, projectKey string) (invProject, error) {
	return m.getProjectFunc(ctx, projectKey)
}

func (m *mockInvReportBbsAPI) GetRepos(ctx context.Context, projectKey string) ([]invRepository, error) {
	return m.getReposFunc(ctx, projectKey)
}

func (m *mockInvReportBbsAPI) GetRepositoryPullRequests(ctx context.Context, projectKey, repo string) ([]invPullRequest, error) {
	return m.getRepositoryPullRequestsFunc(ctx, projectKey, repo)
}

func (m *mockInvReportBbsAPI) GetRepositoryLatestCommitDate(ctx context.Context, projectKey, repo string) (*time.Time, error) {
	return m.getRepositoryLatestCommitDateFunc(ctx, projectKey, repo)
}

func (m *mockInvReportBbsAPI) GetRepositoryAndAttachmentsSize(ctx context.Context, projectKey, repo string) (uint64, uint64, error) {
	return m.getRepositoryAndAttachmentsSizeFunc(ctx, projectKey, repo)
}

func (m *mockInvReportBbsAPI) GetIsRepositoryArchived(ctx context.Context, projectKey, repo string) (bool, error) {
	return m.getIsRepositoryArchivedFunc(ctx, projectKey, repo)
}

// ---------------------------------------------------------------------------
// Test constants (prefixed inv* to avoid collisions with migrate_repo_test.go)
// ---------------------------------------------------------------------------

const (
	invBbsServerURL = "http://bbs-server-url"
	invFooProject   = "project1"
	invBarProject   = "project2"
	invFooKey       = "FP"
	invBarKey       = "BP"
	invRepoName     = "foo-repo"
	invRepoSlug     = "foo-repo-slug"
	invRepoSize     = uint64(10000)
	invAttachSize   = uint64(10000)

	invReposCSVBaseHeader = "project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes"
)

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func defaultInvReportMock() *mockInvReportBbsAPI {
	lastCommit := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	return &mockInvReportBbsAPI{
		getProjectsFunc: func(_ context.Context) ([]invProject, error) {
			return []invProject{{ID: 1, Key: invFooKey, Name: invFooProject}}, nil
		},
		getProjectFunc: func(_ context.Context, key string) (invProject, error) {
			return invProject{ID: 1, Key: key, Name: invFooProject}, nil
		},
		getReposFunc: func(_ context.Context, _ string) ([]invRepository, error) {
			return []invRepository{{ID: 1, Slug: invRepoSlug, Name: invRepoName}}, nil
		},
		getRepositoryPullRequestsFunc: func(_ context.Context, _, _ string) ([]invPullRequest, error) {
			return make([]invPullRequest, 5), nil
		},
		getRepositoryLatestCommitDateFunc: func(_ context.Context, _, _ string) (*time.Time, error) {
			return &lastCommit, nil
		},
		getRepositoryAndAttachmentsSizeFunc: func(_ context.Context, _, _ string) (uint64, uint64, error) {
			return invRepoSize, invAttachSize, nil
		},
		getIsRepositoryArchivedFunc: func(_ context.Context, _, _ string) (bool, error) {
			return false, nil
		},
	}
}

// ---------------------------------------------------------------------------
// Handler / Runner Tests
// ---------------------------------------------------------------------------

func TestBbsInventoryReport_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	api := defaultInvReportMock()

	writtenFiles := make(map[string]string)
	writeFile := func(path, content string) error {
		writtenFiles[path] = content
		return nil
	}

	cmd := newInventoryReportCmd(api, log, writeFile)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--bbs-server-url", invBbsServerURL})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Creating inventory report...")

	// Both CSV files should have been written
	assert.Contains(t, writtenFiles, "projects.csv")
	assert.Contains(t, writtenFiles, "repos.csv")
}

func TestBbsInventoryReport_ScopedToSingleProject(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	api := defaultInvReportMock()

	// When scoped to a project, GetProjects should NOT be called
	api.getProjectsFunc = func(_ context.Context) ([]invProject, error) {
		t.Fatal("GetProjects should not be called when --bbs-project is specified")
		return nil, nil
	}
	api.getProjectFunc = func(_ context.Context, key string) (invProject, error) {
		return invProject{ID: 1, Key: key, Name: invFooProject}, nil
	}

	writtenFiles := make(map[string]string)
	writeFile := func(path, content string) error {
		writtenFiles[path] = content
		return nil
	}

	cmd := newInventoryReportCmd(api, log, writeFile)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--bbs-server-url", invBbsServerURL, "--bbs-project", invFooKey})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Contains(t, writtenFiles, "projects.csv")
	assert.Contains(t, writtenFiles, "repos.csv")
}

func TestBbsInventoryReport_Minimal(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	api := defaultInvReportMock()

	writtenFiles := make(map[string]string)
	writeFile := func(path, content string) error {
		writtenFiles[path] = content
		return nil
	}

	cmd := newInventoryReportCmd(api, log, writeFile)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{"--bbs-server-url", invBbsServerURL, "--minimal"})

	err := cmd.Execute()
	require.NoError(t, err)

	// Minimal projects CSV should NOT have pr-count column
	assert.NotContains(t, writtenFiles["projects.csv"], "pr-count")

	// Minimal repos CSV should NOT have is-archived or pr-count
	assert.NotContains(t, writtenFiles["repos.csv"], "is-archived")
	assert.NotContains(t, writtenFiles["repos.csv"], "pr-count")
}

// ---------------------------------------------------------------------------
// Projects CSV Generator Tests
// ---------------------------------------------------------------------------

func TestGenerateProjectsCSV_OneProject(t *testing.T) {
	api := defaultInvReportMock()
	api.getProjectFunc = func(_ context.Context, _ string) (invProject, error) {
		return invProject{ID: 1, Key: invFooKey, Name: invFooProject}, nil
	}
	api.getReposFunc = func(_ context.Context, _ string) ([]invRepository, error) {
		return make([]invRepository, 82), nil
	}
	api.getRepositoryPullRequestsFunc = func(_ context.Context, _, _ string) ([]invPullRequest, error) {
		return make([]invPullRequest, 10), nil
	}

	result, err := generateProjectsCSV(context.Background(), api, invBbsServerURL, invFooKey, false)
	require.NoError(t, err)

	// 82 repos × 10 PRs each = 820 total PRs
	expected := "project-key,project-name,url,repo-count,pr-count\n"
	expected += fmt.Sprintf(`"%s","%s","%s/projects/%s",%d,%d`, invFooKey, invFooProject, invBbsServerURL, invFooKey, 82, 820)
	expected += "\n"

	assert.Equal(t, expected, result)
}

func TestGenerateProjectsCSV_Minimal(t *testing.T) {
	api := defaultInvReportMock()
	api.getProjectsFunc = func(_ context.Context) ([]invProject, error) {
		return []invProject{
			{ID: 1, Key: invFooKey, Name: invFooProject},
			{ID: 2, Key: invBarKey, Name: invBarProject},
		}, nil
	}
	api.getReposFunc = func(_ context.Context, key string) ([]invRepository, error) {
		if key == invFooKey {
			return make([]invRepository, 82), nil
		}
		return nil, nil
	}

	// PR requests should NOT be called in minimal mode
	api.getRepositoryPullRequestsFunc = func(_ context.Context, _, _ string) ([]invPullRequest, error) {
		t.Fatal("GetRepositoryPullRequests should not be called in minimal mode")
		return nil, nil
	}

	result, err := generateProjectsCSV(context.Background(), api, invBbsServerURL, "", true)
	require.NoError(t, err)

	expected := "project-key,project-name,url,repo-count\n"
	expected += fmt.Sprintf(`"%s","%s","%s/projects/%s",%d`, invFooKey, invFooProject, invBbsServerURL, invFooKey, 82)
	expected += "\n"
	expected += fmt.Sprintf(`"%s","%s","%s/projects/%s",%d`, invBarKey, invBarProject, invBbsServerURL, invBarKey, 0)
	expected += "\n"

	assert.Equal(t, expected, result)
}

// ---------------------------------------------------------------------------
// Repos CSV Generator Tests
// ---------------------------------------------------------------------------

func TestGenerateReposCSV_OneRepo(t *testing.T) {
	lastCommitDate := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	api := defaultInvReportMock()
	api.getProjectFunc = func(_ context.Context, _ string) (invProject, error) {
		return invProject{ID: 1, Key: invFooKey, Name: "project"}, nil
	}
	api.getReposFunc = func(_ context.Context, _ string) ([]invRepository, error) {
		return []invRepository{{ID: 1, Slug: invRepoSlug, Name: invRepoName}}, nil
	}
	api.getRepositoryPullRequestsFunc = func(_ context.Context, _, _ string) ([]invPullRequest, error) {
		return make([]invPullRequest, 822), nil
	}
	api.getRepositoryLatestCommitDateFunc = func(_ context.Context, _, _ string) (*time.Time, error) {
		return &lastCommitDate, nil
	}
	api.getRepositoryAndAttachmentsSizeFunc = func(_ context.Context, _, _ string) (uint64, uint64, error) {
		return invRepoSize, invAttachSize, nil
	}
	api.getIsRepositoryArchivedFunc = func(_ context.Context, _, _ string) (bool, error) {
		return false, nil
	}

	result, err := generateReposCSV(context.Background(), api, invBbsServerURL, invFooKey, false)
	require.NoError(t, err)

	formattedDate := lastCommitDate.Format("2006-01-02 03:04 PM")

	expected := "project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes,is-archived,pr-count\n"
	expected += fmt.Sprintf(`"%s","%s","%s","%s/projects/%s/repos/%s","%s","%d","%d","%s",%d`,
		invFooKey, "project", invRepoName,
		invBbsServerURL, invFooKey, invRepoSlug,
		formattedDate, invRepoSize, invAttachSize,
		"False", 822)
	expected += "\n"

	assert.Equal(t, expected, result)
}

func TestGenerateReposCSV_ArchivedFieldRemoved_OutdatedBBS(t *testing.T) {
	lastCommitDate := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	api := defaultInvReportMock()
	api.getProjectFunc = func(_ context.Context, _ string) (invProject, error) {
		return invProject{ID: 1, Key: invFooKey, Name: "project"}, nil
	}
	api.getReposFunc = func(_ context.Context, _ string) ([]invRepository, error) {
		return []invRepository{{ID: 1, Slug: invRepoSlug, Name: invRepoName}}, nil
	}
	api.getRepositoryPullRequestsFunc = func(_ context.Context, _, _ string) ([]invPullRequest, error) {
		return make([]invPullRequest, 822), nil
	}
	api.getRepositoryLatestCommitDateFunc = func(_ context.Context, _, _ string) (*time.Time, error) {
		return &lastCommitDate, nil
	}
	api.getRepositoryAndAttachmentsSizeFunc = func(_ context.Context, _, _ string) (uint64, uint64, error) {
		return invRepoSize, invAttachSize, nil
	}
	// Simulate BBS < 6.0 where archived field is not available
	api.getIsRepositoryArchivedFunc = func(_ context.Context, _, _ string) (bool, error) {
		return false, errors.New("archived field not available")
	}

	result, err := generateReposCSV(context.Background(), api, invBbsServerURL, invFooKey, false)
	require.NoError(t, err)

	formattedDate := lastCommitDate.Format("2006-01-02 03:04 PM")

	// Header should NOT contain is-archived
	assert.NotContains(t, result, "is-archived")

	// But should still have pr-count
	expectedHeader := "project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes,pr-count\n"
	assert.True(t, strings.HasPrefix(result, expectedHeader), "Header mismatch.\nGot: %s", result)

	// Data row
	expectedRow := fmt.Sprintf(`"%s","%s","%s","%s/projects/%s/repos/%s","%s","%d","%d",%d`,
		invFooKey, "project", invRepoName,
		invBbsServerURL, invFooKey, invRepoSlug,
		formattedDate, invRepoSize, invAttachSize,
		822)
	assert.Contains(t, result, expectedRow)
}

func TestGenerateReposCSV_Minimal(t *testing.T) {
	lastCommitDate := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	api := defaultInvReportMock()
	api.getProjectFunc = func(_ context.Context, _ string) (invProject, error) {
		return invProject{ID: 1, Key: invFooKey, Name: "project"}, nil
	}
	api.getReposFunc = func(_ context.Context, _ string) ([]invRepository, error) {
		return []invRepository{{ID: 1, Slug: invRepoSlug, Name: invRepoName}}, nil
	}
	api.getRepositoryLatestCommitDateFunc = func(_ context.Context, _, _ string) (*time.Time, error) {
		return &lastCommitDate, nil
	}
	api.getRepositoryAndAttachmentsSizeFunc = func(_ context.Context, _, _ string) (uint64, uint64, error) {
		return invRepoSize, invAttachSize, nil
	}
	// These should NOT be called in minimal mode
	api.getRepositoryPullRequestsFunc = func(_ context.Context, _, _ string) ([]invPullRequest, error) {
		t.Fatal("GetRepositoryPullRequests should not be called in minimal mode")
		return nil, nil
	}
	api.getIsRepositoryArchivedFunc = func(_ context.Context, _, _ string) (bool, error) {
		t.Fatal("GetIsRepositoryArchived should not be called in minimal mode")
		return false, nil
	}

	result, err := generateReposCSV(context.Background(), api, invBbsServerURL, invFooKey, true)
	require.NoError(t, err)

	formattedDate := lastCommitDate.Format("2006-01-02 03:04 PM")

	expected := invReposCSVBaseHeader + "\n"
	expected += fmt.Sprintf(`"%s","%s","%s","%s/projects/%s/repos/%s","%s","%d","%d"`,
		invFooKey, "project", invRepoName,
		invBbsServerURL, invFooKey, invRepoSlug,
		formattedDate, invRepoSize, invAttachSize)
	expected += "\n"

	assert.Equal(t, expected, result)
}

func TestGenerateReposCSV_NullLatestCommitDate(t *testing.T) {
	api := defaultInvReportMock()
	api.getProjectFunc = func(_ context.Context, _ string) (invProject, error) {
		return invProject{ID: 1, Key: invFooKey, Name: "project%2Cname"}, nil
	}
	api.getReposFunc = func(_ context.Context, _ string) ([]invRepository, error) {
		return []invRepository{{ID: 1, Slug: invRepoSlug, Name: "repo%2Cname"}}, nil
	}
	// Return nil for latest commit date
	api.getRepositoryLatestCommitDateFunc = func(_ context.Context, _, _ string) (*time.Time, error) {
		return nil, nil
	}
	api.getRepositoryAndAttachmentsSizeFunc = func(_ context.Context, _, _ string) (uint64, uint64, error) {
		return invRepoSize, invAttachSize, nil
	}

	result, err := generateReposCSV(context.Background(), api, invBbsServerURL, invFooKey, true)
	require.NoError(t, err)

	expected := invReposCSVBaseHeader + "\n"
	// Note: empty field between double commas for null date
	expected += fmt.Sprintf(`"%s","%s","%s","%s/projects/%s/repos/%s",,"%d","%d"`,
		invFooKey, "project%2Cname", "repo%2Cname",
		invBbsServerURL, invFooKey, invRepoSlug,
		invRepoSize, invAttachSize)
	expected += "\n"

	assert.Equal(t, expected, result)
}

func TestGenerateReposCSV_EscapeProjectAndRepoNames(t *testing.T) {
	lastCommitDate := time.Date(2023, 6, 15, 14, 30, 0, 0, time.UTC)

	api := defaultInvReportMock()
	api.getProjectFunc = func(_ context.Context, _ string) (invProject, error) {
		return invProject{ID: 1, Key: invFooKey, Name: "project,name"}, nil
	}
	api.getReposFunc = func(_ context.Context, _ string) ([]invRepository, error) {
		return []invRepository{{ID: 1, Slug: invRepoSlug, Name: "repo,name"}}, nil
	}
	api.getRepositoryLatestCommitDateFunc = func(_ context.Context, _, _ string) (*time.Time, error) {
		return &lastCommitDate, nil
	}
	api.getRepositoryAndAttachmentsSizeFunc = func(_ context.Context, _, _ string) (uint64, uint64, error) {
		return invRepoSize, invAttachSize, nil
	}

	result, err := generateReposCSV(context.Background(), api, invBbsServerURL, invFooKey, true)
	require.NoError(t, err)

	formattedDate := lastCommitDate.Format("2006-01-02 03:04 PM")

	expected := invReposCSVBaseHeader + "\n"
	expected += fmt.Sprintf(`"%s","%s","%s","%s/projects/%s/repos/%s","%s","%d","%d"`,
		invFooKey, "project%2Cname", "repo%2Cname",
		invBbsServerURL, invFooKey, invRepoSlug,
		formattedDate, invRepoSize, invAttachSize)
	expected += "\n"

	assert.Equal(t, expected, result)
}
