package ado

import (
	"encoding/json"
	"strings"
	"time"
)

// TeamProject represents an Azure DevOps team project.
type TeamProject struct {
	ID   string `json:"id"`
	Name string `json:"name"`
}

// Repository represents an Azure DevOps repository.
type Repository struct {
	ID         string `json:"id"`
	Name       string `json:"name"`
	Size       uint64 `json:"size"`
	IsDisabled bool   `json:"isDisabled"`
}

// BoardsConnection holds an Azure Boards ↔ GitHub external connection.
type BoardsConnection struct {
	ConnectionID   string
	EndpointID     string
	ConnectionName string
	RepoIDs        []string
}

// PipelineInfo captures the mutable settings of a pipeline definition.
type PipelineInfo struct {
	DefaultBranch      string
	Clean              string
	CheckoutSubmodules string
	Triggers           json.RawMessage
}

// PipelineRepository describes the repository linked to a pipeline.
type PipelineRepository struct {
	RepoName           string
	RepoID             string
	DefaultBranch      string
	Clean              string
	CheckoutSubmodules string
}

// BuildStatus is the current status/result of a single build.
type BuildStatus struct {
	Status string
	Result string
	URL    string
}

// Build is a build record with timing information.
type Build struct {
	BuildID   int
	Status    string
	Result    string
	URL       string
	QueueTime time.Time
}

// repoIDKey is the cache key for repository ID lookups.
type repoIDKey struct {
	org         string
	teamProject string
}

// pipelineIDKey is the cache key for pipeline ID lookups.
type pipelineIDKey struct {
	org          string
	teamProject  string
	pipelinePath string
}

// ---------------------------------------------------------------------------
// Branch policy models (for AdoPipelineTriggerService)
// ---------------------------------------------------------------------------

// BranchPolicy represents an Azure DevOps branch policy configuration.
type BranchPolicy struct {
	ID        string               `json:"id"`
	Type      PolicyType           `json:"type"`
	IsEnabled bool                 `json:"isEnabled"`
	Settings  BranchPolicySettings `json:"settings"`
}

// PolicyType represents the type information for an ADO policy.
type PolicyType struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
}

// BranchPolicySettings represents settings for an ADO branch policy.
type BranchPolicySettings struct {
	BuildDefinitionId       string  `json:"buildDefinitionId"`
	DisplayName             string  `json:"displayName"`
	QueueOnSourceUpdateOnly bool    `json:"queueOnSourceUpdateOnly"`
	ManualQueueOnly         bool    `json:"manualQueueOnly"`
	ValidDuration           float64 `json:"validDuration"`
}

// BranchPolicyResponse is the wrapper for ADO branch policy list responses.
type BranchPolicyResponse struct {
	Value []BranchPolicy `json:"value"`
	Count int            `json:"count"`
}

// ---------------------------------------------------------------------------
// Pipeline test result models
// ---------------------------------------------------------------------------

// PipelineTestResult captures the outcome of testing a single pipeline.
type PipelineTestResult struct {
	AdoOrg               string     `json:"adoOrg"`
	AdoTeamProject       string     `json:"adoTeamProject"`
	AdoRepoName          string     `json:"adoRepoName"`
	PipelineName         string     `json:"pipelineName"`
	PipelineId           int        `json:"pipelineId"`
	PipelineUrl          string     `json:"pipelineUrl"`
	BuildId              int        `json:"buildId,omitempty"`
	BuildUrl             string     `json:"buildUrl,omitempty"`
	Status               string     `json:"status"`
	Result               string     `json:"result"`
	StartTime            time.Time  `json:"startTime"`
	EndTime              *time.Time `json:"endTime,omitempty"`
	ErrorMessage         string     `json:"errorMessage,omitempty"`
	RewiredSuccessfully  bool       `json:"rewiredSuccessfully"`
	RestoredSuccessfully bool       `json:"restoredSuccessfully"`
}

// BuildDuration returns the duration of the test, or zero if not yet ended.
func (r *PipelineTestResult) BuildDuration() time.Duration {
	if r.EndTime == nil {
		return 0
	}
	return r.EndTime.Sub(r.StartTime)
}

// IsSuccessful returns true if the build succeeded or partially succeeded.
func (r *PipelineTestResult) IsSuccessful() bool {
	return strings.EqualFold(r.Result, "succeeded") || strings.EqualFold(r.Result, "partiallySucceeded")
}

// IsFailed returns true if the build failed or was canceled.
func (r *PipelineTestResult) IsFailed() bool {
	return strings.EqualFold(r.Result, "failed") || strings.EqualFold(r.Result, "canceled")
}

// IsCompleted returns true if the build has a result.
func (r *PipelineTestResult) IsCompleted() bool {
	return r.Result != ""
}

// IsRunning returns true if the build is still in progress or not started.
func (r *PipelineTestResult) IsRunning() bool {
	return strings.EqualFold(r.Status, "inProgress") || strings.EqualFold(r.Status, "notStarted")
}

// PipelineTestSummary aggregates results from testing multiple pipelines.
type PipelineTestSummary struct {
	TotalPipelines   int                  `json:"totalPipelines"`
	SuccessfulBuilds int                  `json:"successfulBuilds"`
	FailedBuilds     int                  `json:"failedBuilds"`
	TimedOutBuilds   int                  `json:"timedOutBuilds"`
	ErrorsRewiring   int                  `json:"errorsRewiring"`
	ErrorsRestoring  int                  `json:"errorsRestoring"`
	TotalTestTime    time.Duration        `json:"totalTestTime"`
	Results          []PipelineTestResult `json:"results"`
}

// SuccessRate returns the percentage of successful builds.
func (s *PipelineTestSummary) SuccessRate() float64 {
	if s.TotalPipelines == 0 {
		return 0
	}
	return float64(s.SuccessfulBuilds) / float64(s.TotalPipelines) * 100
}

// AddResult appends a single result.
func (s *PipelineTestSummary) AddResult(r PipelineTestResult) {
	s.Results = append(s.Results, r)
}

// AddResults appends multiple results.
func (s *PipelineTestSummary) AddResults(results []PipelineTestResult) {
	s.Results = append(s.Results, results...)
}
