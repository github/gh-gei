package ado

import (
	"encoding/json"
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
	Size       uint64 `json:"size,string"` // ADO returns size as string in paginated response
	IsDisabled bool   `json:"isDisabled,string"`
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
