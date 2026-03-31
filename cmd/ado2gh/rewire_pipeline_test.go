package main

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"testing"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockRewirePipelineAPI struct {
	getPipelineIdFn func(ctx context.Context, org, teamProject, pipeline string) (int, error)
	getPipelineFn   func(ctx context.Context, org, teamProject string, pipelineId int) (ado.PipelineInfo, error)
}

func (m *mockRewirePipelineAPI) GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error) {
	if m.getPipelineIdFn != nil {
		return m.getPipelineIdFn(ctx, org, teamProject, pipeline)
	}
	return 0, nil
}

func (m *mockRewirePipelineAPI) GetPipeline(ctx context.Context, org, teamProject string, pipelineId int) (ado.PipelineInfo, error) {
	if m.getPipelineFn != nil {
		return m.getPipelineFn(ctx, org, teamProject, pipelineId)
	}
	return ado.PipelineInfo{}, nil
}

type mockRewireTriggerService struct {
	rewirePipelineToGitHubFn func(ctx context.Context, adoOrg, teamProject string, pipelineId int, defaultBranch, clean, checkoutSubmodules string, githubOrg, githubRepo, connectedServiceId string, originalTriggers json.RawMessage, targetApiUrl string) (bool, error)

	rewireCalled       bool
	rewireAdoOrg       string
	rewireTeamProject  string
	rewirePipelineId   int
	rewireGithubOrg    string
	rewireGithubRepo   string
	rewireConnSvcId    string
	rewireTargetApiUrl string
}

func (m *mockRewireTriggerService) RewirePipelineToGitHub(ctx context.Context, adoOrg, teamProject string, pipelineId int, defaultBranch, clean, checkoutSubmodules string, githubOrg, githubRepo, connectedServiceId string, originalTriggers json.RawMessage, targetApiUrl string) (bool, error) {
	m.rewireCalled = true
	m.rewireAdoOrg = adoOrg
	m.rewireTeamProject = teamProject
	m.rewirePipelineId = pipelineId
	m.rewireGithubOrg = githubOrg
	m.rewireGithubRepo = githubRepo
	m.rewireConnSvcId = connectedServiceId
	m.rewireTargetApiUrl = targetApiUrl
	if m.rewirePipelineToGitHubFn != nil {
		return m.rewirePipelineToGitHubFn(ctx, adoOrg, teamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, connectedServiceId, originalTriggers, targetApiUrl)
	}
	return true, nil
}

type mockRewireTestService struct {
	testPipelineFn func(ctx context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error)
	testCalled     bool
}

func (m *mockRewireTestService) TestPipeline(ctx context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
	m.testCalled = true
	if m.testPipelineFn != nil {
		return m.testPipelineFn(ctx, args)
	}
	return ado.PipelineTestResult{}, nil
}

type mockRewireEnv struct {
	adoPAT string
}

func (m *mockRewireEnv) ADOPAT() string { return m.adoPAT }

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestRewirePipeline_HappyPath_PipelineName(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockRewirePipelineAPI{
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 42, nil
		},
		getPipelineFn: func(_ context.Context, _, _ string, _ int) (ado.PipelineInfo, error) {
			return ado.PipelineInfo{
				DefaultBranch:      "main",
				Clean:              "true",
				CheckoutSubmodules: "false",
				Triggers:           json.RawMessage(`[]`),
			}, nil
		},
	}
	triggerSvc := &mockRewireTriggerService{}
	testSvc := &mockRewireTestService{}

	cmd := newRewirePipelineCmd(adoAPI, triggerSvc, testSvc, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline", "my-pipeline",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Rewiring Pipeline to GitHub repo...")
	assert.Contains(t, output, "Successfully rewired pipeline")

	assert.True(t, triggerSvc.rewireCalled)
	assert.Equal(t, "my-org", triggerSvc.rewireAdoOrg)
	assert.Equal(t, "my-project", triggerSvc.rewireTeamProject)
	assert.Equal(t, 42, triggerSvc.rewirePipelineId)
	assert.Equal(t, "gh-org", triggerSvc.rewireGithubOrg)
	assert.Equal(t, "gh-repo", triggerSvc.rewireGithubRepo)
	assert.Equal(t, "svc-conn-id", triggerSvc.rewireConnSvcId)
}

func TestRewirePipeline_HappyPath_PipelineId(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockRewirePipelineAPI{
		getPipelineFn: func(_ context.Context, _, _ string, id int) (ado.PipelineInfo, error) {
			assert.Equal(t, 99, id)
			return ado.PipelineInfo{
				DefaultBranch:      "develop",
				Clean:              "false",
				CheckoutSubmodules: "true",
				Triggers:           json.RawMessage(`[]`),
			}, nil
		},
	}
	triggerSvc := &mockRewireTriggerService{}
	testSvc := &mockRewireTestService{}

	cmd := newRewirePipelineCmd(adoAPI, triggerSvc, testSvc, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline-id", "99",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.True(t, triggerSvc.rewireCalled)
	assert.Equal(t, 99, triggerSvc.rewirePipelineId)
	assert.Contains(t, buf.String(), "Using provided pipeline ID: 99")
}

func TestRewirePipeline_DryRun_Succeeded(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	testSvc := &mockRewireTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			assert.Equal(t, "my-org", args.AdoOrg)
			assert.Equal(t, "my-project", args.AdoTeamProject)
			assert.Equal(t, "my-pipeline", args.PipelineName)
			assert.Equal(t, "gh-org", args.GithubOrg)
			assert.Equal(t, "gh-repo", args.GithubRepo)
			assert.Equal(t, "svc-conn-id", args.ServiceConnectionId)
			return ado.PipelineTestResult{
				AdoOrg:         "my-org",
				AdoTeamProject: "my-project",
				PipelineName:   "my-pipeline",
				Result:         "succeeded",
			}, nil
		},
	}

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, testSvc, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline", "my-pipeline",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--dry-run",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Starting dry-run mode")
	assert.Contains(t, output, "PIPELINE TEST REPORT")
	assert.Contains(t, output, "Pipeline test PASSED")
	assert.True(t, testSvc.testCalled)
}

func TestRewirePipeline_DryRun_Failed(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	testSvc := &mockRewireTestService{
		testPipelineFn: func(_ context.Context, _ ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			return ado.PipelineTestResult{
				AdoOrg:         "my-org",
				AdoTeamProject: "my-project",
				PipelineName:   "my-pipeline",
				Result:         "failed",
			}, nil
		},
	}

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, testSvc, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline", "my-pipeline",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--dry-run",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	output := buf.String()
	assert.Contains(t, output, "Pipeline test FAILED")
}

func TestRewirePipeline_DryRun_PipelineId(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	testSvc := &mockRewireTestService{
		testPipelineFn: func(_ context.Context, args ado.PipelineTestArgs) (ado.PipelineTestResult, error) {
			require.NotNil(t, args.PipelineId)
			assert.Equal(t, 55, *args.PipelineId)
			return ado.PipelineTestResult{
				PipelineName: "auto-resolved",
				Result:       "succeeded",
			}, nil
		},
	}

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, testSvc, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline-id", "55",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--dry-run",
	})

	err := cmd.Execute()
	require.NoError(t, err)
	assert.True(t, testSvc.testCalled)
}

func TestRewirePipeline_MissingRequiredFlags(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, &mockRewireTestService{}, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		// missing --ado-org and others
		"--ado-pipeline", "my-pipeline",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "--ado-org")
}

func TestRewirePipeline_NeitherPipelineNameNorId(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, &mockRewireTestService{}, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "either --ado-pipeline or --ado-pipeline-id must be specified")
}

func TestRewirePipeline_BothPipelineNameAndId(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, &mockRewireTestService{}, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline", "my-pipeline",
		"--ado-pipeline-id", "42",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "only one of")
}

func TestRewirePipeline_InvalidPipelineId(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	cmd := newRewirePipelineCmd(&mockRewirePipelineAPI{}, &mockRewireTriggerService{}, &mockRewireTestService{}, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline-id", "not-a-number",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "must be a valid integer")
}

func TestRewirePipeline_PipelineLookupFails(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockRewirePipelineAPI{
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 0, errors.New("pipeline not found")
		},
	}

	cmd := newRewirePipelineCmd(adoAPI, &mockRewireTriggerService{}, &mockRewireTestService{}, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline", "nonexistent",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
	})

	err := cmd.Execute()
	require.Error(t, err)
	assert.Contains(t, err.Error(), "pipeline lookup failed")
}

func TestRewirePipeline_TargetApiUrl(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	adoAPI := &mockRewirePipelineAPI{
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 10, nil
		},
		getPipelineFn: func(_ context.Context, _, _ string, _ int) (ado.PipelineInfo, error) {
			return ado.PipelineInfo{
				DefaultBranch: "main",
				Triggers:      json.RawMessage(`[]`),
			}, nil
		},
	}
	triggerSvc := &mockRewireTriggerService{}

	cmd := newRewirePipelineCmd(adoAPI, triggerSvc, &mockRewireTestService{}, &mockRewireEnv{adoPAT: "token"}, log)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)
	cmd.SetArgs([]string{
		"--ado-org", "my-org",
		"--ado-team-project", "my-project",
		"--ado-pipeline", "my-pipeline",
		"--github-org", "gh-org",
		"--github-repo", "gh-repo",
		"--service-connection-id", "svc-conn-id",
		"--target-api-url", "https://ghes.example.com/api/v3",
	})

	err := cmd.Execute()
	require.NoError(t, err)

	assert.True(t, triggerSvc.rewireCalled)
	assert.Equal(t, "https://ghes.example.com/api/v3", triggerSvc.rewireTargetApiUrl)
}
