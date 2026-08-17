package main

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"io"
	"os"
	"path/filepath"
	"sync"
	"testing"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// syncWriter wraps an io.Writer with a mutex for safe concurrent use.
type syncWriter struct {
	mu sync.Mutex
	w  io.Writer
}

func (sw *syncWriter) Write(p []byte) (n int, err error) {
	sw.mu.Lock()
	defer sw.mu.Unlock()
	return sw.w.Write(p)
}

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockTestPipelinesAPI struct {
	getEnabledReposFn func(ctx context.Context, org, teamProject string) ([]ado.Repository, error)
	getPipelinesFn    func(ctx context.Context, org, teamProject, repoId string) ([]string, error)
	getPipelineIdFn   func(ctx context.Context, org, teamProject, pipeline string) (int, error)
}

func (m *mockTestPipelinesAPI) GetEnabledRepos(ctx context.Context, org, teamProject string) ([]ado.Repository, error) {
	if m.getEnabledReposFn != nil {
		return m.getEnabledReposFn(ctx, org, teamProject)
	}
	return nil, nil
}

func (m *mockTestPipelinesAPI) GetPipelines(ctx context.Context, org, teamProject, repoId string) ([]string, error) {
	if m.getPipelinesFn != nil {
		return m.getPipelinesFn(ctx, org, teamProject, repoId)
	}
	return nil, nil
}

func (m *mockTestPipelinesAPI) GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error) {
	if m.getPipelineIdFn != nil {
		return m.getPipelineIdFn(ctx, org, teamProject, pipeline)
	}
	return 0, nil
}

type mockTestPipelinesTestService struct {
	testPipelineFn  func(ctx context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error)
	mu              sync.Mutex
	callCount       int
	calledPipelines []string
}

func (m *mockTestPipelinesTestService) TestPipeline(ctx context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
	m.mu.Lock()
	m.callCount++
	m.calledPipelines = append(m.calledPipelines, args.PipelineName)
	m.mu.Unlock()
	if m.testPipelineFn != nil {
		return m.testPipelineFn(ctx, args)
	}
	return ado.PipelineTestResult{}, nil
}

func (m *mockTestPipelinesTestService) getCallCount() int {
	m.mu.Lock()
	defer m.mu.Unlock()
	return m.callCount
}

type mockTestPipelinesEnv struct {
	adoPAT string
}

func (m *mockTestPipelinesEnv) ADOPAT() string { return m.adoPAT }

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestTestPipelines_HappyPath(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	reportPath := filepath.Join(t.TempDir(), "report.json")

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{
				{ID: "repo-1", Name: "RepoA"},
			}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, repoId string) ([]string, error) {
			if repoId == "repo-1" {
				return []string{`\build-pipeline`, `\deploy-pipeline`}, nil
			}
			return nil, nil
		},
		getPipelineIdFn: func(_ context.Context, _, _, pipeline string) (int, error) {
			switch pipeline {
			case `\build-pipeline`:
				return 1, nil
			case `\deploy-pipeline`:
				return 2, nil
			}
			return 0, errors.New("unknown pipeline")
		},
	}

	testSvc := &mockTestPipelinesTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				AdoOrg:               args.AdoOrg,
				AdoTeamProject:       args.AdoTeamProject,
				PipelineName:         args.PipelineName,
				PipelineId:           *args.PipelineId,
				Result:               "succeeded",
				RewiredSuccessfully:  true,
				RestoredSuccessfully: true,
			}, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, testSvc, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--report-path", reportPath,
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Starting batch pipeline testing...")
	assert.Contains(t, output, "Found 2 pipelines to test")
	assert.Contains(t, output, "PIPELINE BATCH TEST SUMMARY")
	assert.Contains(t, output, "Total Pipelines Tested: 2")
	assert.Contains(t, output, "Successful Builds: 2")
	assert.Contains(t, output, "Batch testing completed")

	assert.Equal(t, 2, testSvc.getCallCount())

	// Verify JSON report was written
	data, err := os.ReadFile(reportPath)
	require.NoError(t, err)
	var summary ado.PipelineTestSummary
	require.NoError(t, json.Unmarshal(data, &summary))
	assert.Equal(t, 2, summary.TotalPipelines)
	assert.Len(t, summary.Results, 2)
}

func TestTestPipelines_NoPipelinesFound(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{
				{ID: "repo-1", Name: "EmptyRepo"},
			}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, _ string) ([]string, error) {
			return nil, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, &mockTestPipelinesTestService{}, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Contains(t, buf.String(), "No pipelines found matching the criteria")
}

func TestTestPipelines_PipelineFilter(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	reportPath := filepath.Join(t.TempDir(), "report.json")

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{{ID: "r1", Name: "Repo"}}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{`\build-main`, `\deploy-staging`, `\build-feature`}, nil
		},
		getPipelineIdFn: func(_ context.Context, _, _, pipeline string) (int, error) {
			switch pipeline {
			case `\build-main`:
				return 10, nil
			case `\build-feature`:
				return 30, nil
			}
			return 0, errors.New("unexpected pipeline: " + pipeline)
		},
	}

	testSvc := &mockTestPipelinesTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				PipelineName:         args.PipelineName,
				PipelineId:           *args.PipelineId,
				Result:               "succeeded",
				RewiredSuccessfully:  true,
				RestoredSuccessfully: true,
			}, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, testSvc, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--pipeline-filter", `\build-*`,
		"--report-path", reportPath,
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.Equal(t, 2, testSvc.getCallCount())
	assert.Contains(t, buf.String(), "Found 2 pipelines to test")
}

func TestTestPipelines_WildcardFilterQuestionMark(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	reportPath := filepath.Join(t.TempDir(), "report.json")

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{{ID: "r1", Name: "Repo"}}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"pipeline-a", "pipeline-b", "pipeline-cd"}, nil
		},
		getPipelineIdFn: func(_ context.Context, _, _, pipeline string) (int, error) {
			switch pipeline {
			case "pipeline-a":
				return 1, nil
			case "pipeline-b":
				return 2, nil
			}
			return 0, errors.New("unexpected")
		},
	}

	testSvc := &mockTestPipelinesTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				PipelineName:         args.PipelineName,
				RewiredSuccessfully:  true,
				RestoredSuccessfully: true,
			}, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, testSvc, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--pipeline-filter", "pipeline-?",
		"--report-path", reportPath,
	})

	err := cmd.Execute()
	require.NoError(t, err)

	// pipeline-a and pipeline-b match, pipeline-cd does not
	assert.Equal(t, 2, testSvc.getCallCount())
}

func TestTestPipelines_MissingRequiredFlags(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	cmd := newTestPipelinesCmd(&mockTestPipelinesAPI{}, &mockTestPipelinesTestService{}, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		// missing other required flags
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--ado-team-project")
}

func TestTestPipelines_DiscoveryErrorContinues(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	reportPath := filepath.Join(t.TempDir(), "report.json")

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{
				{ID: "r1", Name: "Good"},
				{ID: "r2", Name: "Bad"},
			}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, repoId string) ([]string, error) {
			if repoId == "r2" {
				return nil, errors.New("permission denied")
			}
			return []string{"pipeline-ok"}, nil
		},
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 100, nil
		},
	}

	testSvc := &mockTestPipelinesTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				PipelineName:         args.PipelineName,
				Result:               "succeeded",
				RewiredSuccessfully:  true,
				RestoredSuccessfully: true,
			}, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, testSvc, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--report-path", reportPath,
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Could not get pipelines for repository 'Bad'")
	assert.Contains(t, output, "Found 1 pipelines to test")
	assert.Equal(t, 1, testSvc.getCallCount())
}

func TestTestPipelines_GeneratesJSONReport(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	reportPath := filepath.Join(t.TempDir(), "report.json")

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{{ID: "r1", Name: "Repo"}}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"my-pipeline"}, nil
		},
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 42, nil
		},
	}

	testSvc := &mockTestPipelinesTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				AdoOrg:               "my-org",
				AdoTeamProject:       "my-project",
				PipelineName:         "my-pipeline",
				PipelineId:           42,
				Result:               "failed",
				RewiredSuccessfully:  true,
				RestoredSuccessfully: true,
			}, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, testSvc, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--report-path", reportPath,
	})

	err := cmd.Execute()
	require.NoError(t, err)

	data, err := os.ReadFile(reportPath)
	require.NoError(t, err)

	var summary ado.PipelineTestSummary
	require.NoError(t, json.Unmarshal(data, &summary))
	assert.Equal(t, 1, summary.TotalPipelines)
	assert.Equal(t, 0, summary.SuccessfulBuilds)
	assert.Equal(t, 1, summary.FailedBuilds)
	assert.Len(t, summary.Results, 1)
	assert.Equal(t, "my-pipeline", summary.Results[0].PipelineName)
	assert.Equal(t, "failed", summary.Results[0].Result)
}

func TestTestPipelines_RestorationErrorsWarning(t *testing.T) {
	var buf bytes.Buffer
	sw := &syncWriter{w: &buf}
	log := logger.New(false, sw)

	reportPath := filepath.Join(t.TempDir(), "report.json")

	adoAPI := &mockTestPipelinesAPI{
		getEnabledReposFn: func(_ context.Context, _, _ string) ([]ado.Repository, error) {
			return []ado.Repository{{ID: "r1", Name: "Repo"}}, nil
		},
		getPipelinesFn: func(_ context.Context, _, _, _ string) ([]string, error) {
			return []string{"broken-pipeline"}, nil
		},
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 1, nil
		},
	}

	testSvc := &mockTestPipelinesTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				AdoOrg:               "my-org",
				AdoTeamProject:       "my-project",
				PipelineName:         "broken-pipeline",
				PipelineId:           1,
				Result:               "succeeded",
				RewiredSuccessfully:  true,
				RestoredSuccessfully: false,
			}, nil
		},
	}

	cmd := newTestPipelinesCmd(adoAPI, testSvc, &mockTestPipelinesEnv{adoPAT: "token"}, log)
	cmd.SetOut(sw)
	cmd.SetErr(sw)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--report-path", reportPath,
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "PIPELINES REQUIRING MANUAL RESTORATION")
	assert.Contains(t, output, "broken-pipeline")
	assert.Contains(t, output, "Restoration Errors: 1")
}

// ---------------------------------------------------------------------------
// Wildcard matching unit tests
// ---------------------------------------------------------------------------

func TestMatchWildcard(t *testing.T) {
	tests := []struct {
		text    string
		pattern string
		want    bool
	}{
		{"anything", "", true},
		{"anything", "*", true},
		{"build-main", "build-*", true},
		{"build-main", "deploy-*", false},
		{"pipeline-a", "pipeline-?", true},
		{"pipeline-ab", "pipeline-?", false},
		{"Build-Main", "build-*", true}, // case-insensitive
		{`\build\ci`, `\build\*`, true},
	}

	for _, tt := range tests {
		t.Run(tt.text+"_"+tt.pattern, func(t *testing.T) {
			assert.Equal(t, tt.want, matchWildcard(tt.text, tt.pattern))
		})
	}
}
