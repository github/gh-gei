package ado

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"testing"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockPipelineTestAPI struct {
	getPipelineIdFn         func(ctx context.Context, org, teamProject, pipeline string) (int, error)
	isPipelineEnabledFn     func(ctx context.Context, org, teamProject string, pipelineId int) (bool, error)
	getPipelineRepositoryFn func(ctx context.Context, org, teamProject string, pipelineId int) (PipelineRepository, error)
	getPipelineFn           func(ctx context.Context, org, teamProject string, pipelineId int) (PipelineInfo, error)
	queueBuildFn            func(ctx context.Context, org, teamProject string, pipelineId int, sourceBranch string) (int, error)
	getBuildStatusFn        func(ctx context.Context, org, teamProject string, buildId int) (BuildStatus, error)
	restorePipelineFn       func(ctx context.Context, org, teamProject string, pipelineId int, adoRepoName, defaultBranch, clean, checkoutSubmodules string, originalTriggers json.RawMessage) error

	restoreCalled bool
}

func (m *mockPipelineTestAPI) GetPipelineId(ctx context.Context, org, teamProject, pipeline string) (int, error) {
	return m.getPipelineIdFn(ctx, org, teamProject, pipeline)
}

func (m *mockPipelineTestAPI) IsPipelineEnabled(ctx context.Context, org, teamProject string, pipelineId int) (bool, error) {
	return m.isPipelineEnabledFn(ctx, org, teamProject, pipelineId)
}

func (m *mockPipelineTestAPI) GetPipelineRepository(ctx context.Context, org, teamProject string, pipelineId int) (PipelineRepository, error) {
	return m.getPipelineRepositoryFn(ctx, org, teamProject, pipelineId)
}

func (m *mockPipelineTestAPI) GetPipeline(ctx context.Context, org, teamProject string, pipelineId int) (PipelineInfo, error) {
	return m.getPipelineFn(ctx, org, teamProject, pipelineId)
}

func (m *mockPipelineTestAPI) QueueBuild(ctx context.Context, org, teamProject string, pipelineId int, sourceBranch string) (int, error) {
	return m.queueBuildFn(ctx, org, teamProject, pipelineId, sourceBranch)
}

func (m *mockPipelineTestAPI) GetBuildStatus(ctx context.Context, org, teamProject string, buildId int) (BuildStatus, error) {
	return m.getBuildStatusFn(ctx, org, teamProject, buildId)
}

func (m *mockPipelineTestAPI) RestorePipelineToAdoRepo(ctx context.Context, org, teamProject string, pipelineId int, adoRepoName, defaultBranch, clean, checkoutSubmodules string, originalTriggers json.RawMessage) error {
	m.restoreCalled = true
	return m.restorePipelineFn(ctx, org, teamProject, pipelineId, adoRepoName, defaultBranch, clean, checkoutSubmodules, originalTriggers)
}

type mockPipelineRewirer struct {
	rewireFn     func(ctx context.Context, adoOrg, teamProject string, pipelineId int, defaultBranch, clean, checkoutSubmodules string, githubOrg, githubRepo, connectedServiceId string, originalTriggers json.RawMessage, targetApiUrl string) (bool, error)
	rewireCalled bool
}

func (m *mockPipelineRewirer) RewirePipelineToGitHub(ctx context.Context, adoOrg, teamProject string, pipelineId int, defaultBranch, clean, checkoutSubmodules string, githubOrg, githubRepo, connectedServiceId string, originalTriggers json.RawMessage, targetApiUrl string) (bool, error) {
	m.rewireCalled = true
	return m.rewireFn(ctx, adoOrg, teamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, connectedServiceId, originalTriggers, targetApiUrl)
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func newTestPipelineTestService(api *mockPipelineTestAPI, rewirer *mockPipelineRewirer) (*PipelineTestService, *bytes.Buffer) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := NewPipelineTestService(api, rewirer, log)
	svc.pollInterval = 1 * time.Millisecond
	return svc, &buf
}

func defaultPipelineTestArgs() PipelineTestArgs {
	return PipelineTestArgs{
		AdoOrg:                "my-org",
		AdoTeamProject:        "my-project",
		PipelineName:          "my-pipeline",
		GithubOrg:             "gh-org",
		GithubRepo:            "gh-repo",
		ServiceConnectionId:   "conn-id",
		MonitorTimeoutMinutes: 1,
	}
}

func defaultMockAPI() *mockPipelineTestAPI {
	buildStatusCall := 0
	return &mockPipelineTestAPI{
		getPipelineIdFn: func(_ context.Context, _, _, _ string) (int, error) {
			return 42, nil
		},
		isPipelineEnabledFn: func(_ context.Context, _, _ string, _ int) (bool, error) {
			return true, nil
		},
		getPipelineRepositoryFn: func(_ context.Context, _, _ string, _ int) (PipelineRepository, error) {
			return PipelineRepository{
				RepoName:           "ado-repo",
				RepoID:             "repo-guid",
				DefaultBranch:      "refs/heads/main",
				Clean:              "true",
				CheckoutSubmodules: "false",
			}, nil
		},
		getPipelineFn: func(_ context.Context, _, _ string, _ int) (PipelineInfo, error) {
			return PipelineInfo{
				DefaultBranch:      "main",
				Clean:              "true",
				CheckoutSubmodules: "false",
				Triggers:           json.RawMessage(`[{"triggerType":"continuousIntegration"}]`),
			}, nil
		},
		queueBuildFn: func(_ context.Context, _, _ string, _ int, _ string) (int, error) {
			return 100, nil
		},
		getBuildStatusFn: func(_ context.Context, _, _ string, _ int) (BuildStatus, error) {
			buildStatusCall++
			if buildStatusCall == 1 {
				// First call: return URL but no result yet (used right after QueueBuild)
				return BuildStatus{Status: "inProgress", URL: "https://dev.azure.com/build/100"}, nil
			}
			// Subsequent calls: build completed
			return BuildStatus{Status: "completed", Result: "succeeded"}, nil
		},
		restorePipelineFn: func(_ context.Context, _, _ string, _ int, _, _, _, _ string, _ json.RawMessage) error {
			return nil
		},
	}
}

func defaultMockRewirer() *mockPipelineRewirer {
	return &mockPipelineRewirer{
		rewireFn: func(_ context.Context, _, _ string, _ int, _, _, _, _, _, _ string, _ json.RawMessage, _ string) (bool, error) {
			return true, nil
		},
	}
}

// ---------------------------------------------------------------------------
// Tests: TestPipeline
// ---------------------------------------------------------------------------

func TestTestPipeline_HappyPath(t *testing.T) {
	api := defaultMockAPI()
	rewirer := defaultMockRewirer()
	svc, buf := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err)

	assert.Equal(t, "my-org", result.AdoOrg)
	assert.Equal(t, "my-project", result.AdoTeamProject)
	assert.Equal(t, "my-pipeline", result.PipelineName)
	assert.Equal(t, 42, result.PipelineId)
	assert.Equal(t, "ado-repo", result.AdoRepoName)
	assert.Equal(t, 100, result.BuildId)
	assert.Equal(t, "https://dev.azure.com/build/100", result.BuildUrl)
	assert.Equal(t, "completed", result.Status)
	assert.Equal(t, "succeeded", result.Result)
	assert.True(t, result.RewiredSuccessfully)
	assert.True(t, result.RestoredSuccessfully)
	assert.NotNil(t, result.EndTime)
	assert.Empty(t, result.ErrorMessage)

	assert.True(t, rewirer.rewireCalled)
	assert.True(t, api.restoreCalled)

	_ = buf // logs are available if needed
}

func TestTestPipeline_WithProvidedPipelineId(t *testing.T) {
	getPipelineIdCalled := false
	api := defaultMockAPI()
	api.getPipelineIdFn = func(_ context.Context, _, _, _ string) (int, error) {
		getPipelineIdCalled = true
		return 0, fmt.Errorf("should not be called")
	}
	rewirer := defaultMockRewirer()
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	pipelineId := 77
	args.PipelineId = &pipelineId

	ctx := context.Background()
	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err)

	assert.False(t, getPipelineIdCalled)
	assert.Equal(t, 77, result.PipelineId)
	assert.Contains(t, result.PipelineUrl, "definitionId=77")
}

func TestTestPipeline_DisabledPipeline(t *testing.T) {
	api := defaultMockAPI()
	api.isPipelineEnabledFn = func(_ context.Context, _, _ string, _ int) (bool, error) {
		return false, nil
	}
	rewirer := defaultMockRewirer()
	svc, buf := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err)

	assert.Equal(t, "Pipeline is disabled", result.ErrorMessage)
	assert.NotNil(t, result.EndTime)
	assert.False(t, rewirer.rewireCalled)
	assert.False(t, api.restoreCalled)
	assert.Contains(t, buf.String(), "disabled")
}

func TestTestPipeline_RewireFails(t *testing.T) {
	api := defaultMockAPI()
	rewirer := &mockPipelineRewirer{
		rewireFn: func(_ context.Context, _, _ string, _ int, _, _, _, _, _, _ string, _ json.RawMessage, _ string) (bool, error) {
			return false, fmt.Errorf("rewire network error")
		},
	}
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)

	require.Error(t, err)
	var userErr *cmdutil.UserError
	assert.True(t, errors.As(err, &userErr))
	assert.Contains(t, userErr.Message, "Failed to test pipeline")

	// Rewire failed, so RewiredSuccessfully should be false
	assert.False(t, result.RewiredSuccessfully)
	// Emergency restore should NOT be attempted (rewire didn't succeed)
	assert.False(t, api.restoreCalled)
}

func TestTestPipeline_QueueBuildFailsAfterRewire(t *testing.T) {
	api := defaultMockAPI()
	api.queueBuildFn = func(_ context.Context, _, _ string, _ int, _ string) (int, error) {
		return 0, fmt.Errorf("queue build failed")
	}
	rewirer := defaultMockRewirer()
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)

	require.Error(t, err)
	var userErr *cmdutil.UserError
	assert.True(t, errors.As(err, &userErr))

	// Rewire succeeded, build queue failed → emergency restore should be attempted
	assert.True(t, result.RewiredSuccessfully)
	assert.True(t, api.restoreCalled)
	assert.True(t, result.RestoredSuccessfully)
}

func TestTestPipeline_RestoreFails(t *testing.T) {
	api := defaultMockAPI()
	api.restorePipelineFn = func(_ context.Context, _, _ string, _ int, _, _, _, _ string, _ json.RawMessage) error {
		return fmt.Errorf("restore failed")
	}
	rewirer := defaultMockRewirer()
	svc, buf := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err) // Restore failure doesn't cause an error return

	assert.True(t, result.RewiredSuccessfully)
	assert.False(t, result.RestoredSuccessfully)
	assert.Contains(t, result.ErrorMessage, "Failed to restore")
	assert.Contains(t, buf.String(), "Failed to restore")

	// Build monitoring should still have occurred
	assert.NotEmpty(t, result.Status)
}

func TestTestPipeline_BuildTimesOut(t *testing.T) {
	api := defaultMockAPI()
	// Build never completes
	api.getBuildStatusFn = func(_ context.Context, _, _ string, _ int) (BuildStatus, error) {
		return BuildStatus{Status: "inProgress", URL: "https://dev.azure.com/build/100"}, nil
	}
	rewirer := defaultMockRewirer()
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	args.MonitorTimeoutMinutes = 0 // Immediate timeout
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err)

	assert.Equal(t, "timedOut", result.Status)
	assert.Empty(t, result.Result)
}

func TestTestPipeline_BuildFails(t *testing.T) {
	api := defaultMockAPI()
	buildStatusCall := 0
	api.getBuildStatusFn = func(_ context.Context, _, _ string, _ int) (BuildStatus, error) {
		buildStatusCall++
		if buildStatusCall == 1 {
			return BuildStatus{Status: "inProgress", URL: "https://dev.azure.com/build/100"}, nil
		}
		return BuildStatus{Status: "completed", Result: "failed"}, nil
	}
	rewirer := defaultMockRewirer()
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err)

	assert.Equal(t, "completed", result.Status)
	assert.Equal(t, "failed", result.Result)
	assert.True(t, result.IsFailed())
}

func TestTestPipeline_EmergencyRestoreFails(t *testing.T) {
	restoreCallCount := 0
	api := defaultMockAPI()
	api.queueBuildFn = func(_ context.Context, _, _ string, _ int, _ string) (int, error) {
		return 0, fmt.Errorf("queue build error")
	}
	api.restorePipelineFn = func(_ context.Context, _, _ string, _ int, _, _, _, _ string, _ json.RawMessage) error {
		restoreCallCount++
		return fmt.Errorf("emergency restore also failed")
	}
	rewirer := defaultMockRewirer()
	svc, buf := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	ctx := context.Background()

	result, err := svc.TestPipeline(ctx, args)

	require.Error(t, err)
	assert.False(t, result.RestoredSuccessfully)
	assert.Contains(t, buf.String(), "MANUAL RESTORATION REQUIRED")
}

func TestTestPipeline_GetPipelineIdError(t *testing.T) {
	api := defaultMockAPI()
	api.getPipelineIdFn = func(_ context.Context, _, _, _ string) (int, error) {
		return 0, fmt.Errorf("pipeline not found")
	}
	rewirer := defaultMockRewirer()
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	// Don't set PipelineId so it tries to resolve by name
	ctx := context.Background()

	_, err := svc.TestPipeline(ctx, args)
	require.Error(t, err)
	var userErr *cmdutil.UserError
	assert.True(t, errors.As(err, &userErr))
	assert.Contains(t, userErr.Message, "pipeline not found")
}

func TestTestPipeline_ContextCanceled(t *testing.T) {
	api := defaultMockAPI()
	// Build never completes — will be interrupted by context
	api.getBuildStatusFn = func(_ context.Context, _, _ string, _ int) (BuildStatus, error) {
		return BuildStatus{Status: "inProgress", URL: "https://dev.azure.com/build/100"}, nil
	}
	rewirer := defaultMockRewirer()
	svc, _ := newTestPipelineTestService(api, rewirer)

	args := defaultPipelineTestArgs()
	args.MonitorTimeoutMinutes = 30 // Long timeout

	ctx, cancel := context.WithCancel(context.Background())
	// Cancel after a brief delay
	go func() {
		time.Sleep(10 * time.Millisecond)
		cancel()
	}()

	result, err := svc.TestPipeline(ctx, args)
	require.NoError(t, err)

	assert.Equal(t, "timedOut", result.Status)
}
