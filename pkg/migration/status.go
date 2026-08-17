// Package migration provides constants and helpers for migration status tracking.
package migration

import "strings"

// Repository migration status constants.
const (
	RepoQueued            = "QUEUED"
	RepoInProgress        = "IN_PROGRESS"
	RepoFailed            = "FAILED"
	RepoSucceeded         = "SUCCEEDED"
	RepoPendingValidation = "PENDING_VALIDATION"
	RepoFailedValidation  = "FAILED_VALIDATION"
)

// IsRepoSucceeded returns true if the repository migration state is SUCCEEDED.
func IsRepoSucceeded(state string) bool { return normalize(state) == RepoSucceeded }

// IsRepoPending returns true if the repository migration is still in progress.
func IsRepoPending(state string) bool {
	s := normalize(state)
	return s == RepoQueued || s == RepoInProgress || s == RepoPendingValidation
}

// IsRepoFailed returns true if the repository migration has failed.
// Any state that is neither pending nor succeeded is considered failed.
func IsRepoFailed(state string) bool { return !IsRepoPending(state) && !IsRepoSucceeded(state) }

// Organization migration status constants.
const (
	OrgQueued            = "QUEUED"
	OrgInProgress        = "IN_PROGRESS"
	OrgFailed            = "FAILED"
	OrgSucceeded         = "SUCCEEDED"
	OrgNotStarted        = "NOT_STARTED"
	OrgPostRepoMigration = "POST_REPO_MIGRATION"
	OrgPreRepoMigration  = "PRE_REPO_MIGRATION"
	OrgRepoMigration     = "REPO_MIGRATION"
)

// IsOrgSucceeded returns true if the organization migration state is SUCCEEDED.
func IsOrgSucceeded(state string) bool { return normalize(state) == OrgSucceeded }

// IsOrgPending returns true if the organization migration is still in progress.
func IsOrgPending(state string) bool {
	s := normalize(state)
	return s == OrgQueued || s == OrgInProgress || s == OrgNotStarted ||
		s == OrgPostRepoMigration || s == OrgPreRepoMigration || s == OrgRepoMigration
}

// IsOrgFailed returns true if the organization migration has failed.
// Any state that is neither pending nor succeeded is considered failed.
func IsOrgFailed(state string) bool { return !IsOrgPending(state) && !IsOrgSucceeded(state) }

// IsOrgRepoMigration returns true if the organization migration is in the repo migration phase.
func IsOrgRepoMigration(state string) bool { return normalize(state) == OrgRepoMigration }

func normalize(s string) string { return strings.ToUpper(strings.TrimSpace(s)) }
