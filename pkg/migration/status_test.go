package migration_test

import (
	"testing"

	"github.com/github/gh-gei/pkg/migration"
)

func TestIsRepoSucceeded(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"exact match", "SUCCEEDED", true},
		{"lowercase", "succeeded", true},
		{"mixed case", "Succeeded", true},
		{"with whitespace", "  SUCCEEDED  ", true},
		{"queued", "QUEUED", false},
		{"in progress", "IN_PROGRESS", false},
		{"failed", "FAILED", false},
		{"empty", "", false},
		{"unknown", "UNKNOWN", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsRepoSucceeded(tt.state); got != tt.want {
				t.Errorf("IsRepoSucceeded(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}

func TestIsRepoPending(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"queued", "QUEUED", true},
		{"in progress", "IN_PROGRESS", true},
		{"pending validation", "PENDING_VALIDATION", true},
		{"queued lowercase", "queued", true},
		{"with whitespace", " IN_PROGRESS ", true},
		{"succeeded", "SUCCEEDED", false},
		{"failed", "FAILED", false},
		{"empty", "", false},
		{"unknown", "SOMETHING_ELSE", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsRepoPending(tt.state); got != tt.want {
				t.Errorf("IsRepoPending(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}

func TestIsRepoFailed(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"failed", "FAILED", true},
		{"failed validation", "FAILED_VALIDATION", true},
		{"unknown value", "WEIRD", true},
		{"empty string", "", true},
		{"succeeded", "SUCCEEDED", false},
		{"queued", "QUEUED", false},
		{"in progress", "IN_PROGRESS", false},
		{"pending validation", "PENDING_VALIDATION", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsRepoFailed(tt.state); got != tt.want {
				t.Errorf("IsRepoFailed(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}

func TestIsOrgSucceeded(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"exact match", "SUCCEEDED", true},
		{"lowercase", "succeeded", true},
		{"with whitespace", " SUCCEEDED ", true},
		{"queued", "QUEUED", false},
		{"failed", "FAILED", false},
		{"empty", "", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsOrgSucceeded(tt.state); got != tt.want {
				t.Errorf("IsOrgSucceeded(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}

func TestIsOrgPending(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"queued", "QUEUED", true},
		{"in progress", "IN_PROGRESS", true},
		{"not started", "NOT_STARTED", true},
		{"post repo migration", "POST_REPO_MIGRATION", true},
		{"pre repo migration", "PRE_REPO_MIGRATION", true},
		{"repo migration", "REPO_MIGRATION", true},
		{"lowercase", "queued", true},
		{"with whitespace", " IN_PROGRESS ", true},
		{"succeeded", "SUCCEEDED", false},
		{"failed", "FAILED", false},
		{"empty", "", false},
		{"unknown", "SOMETHING_ELSE", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsOrgPending(tt.state); got != tt.want {
				t.Errorf("IsOrgPending(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}

func TestIsOrgFailed(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"failed", "FAILED", true},
		{"unknown value", "WEIRD", true},
		{"empty string", "", true},
		{"succeeded", "SUCCEEDED", false},
		{"queued", "QUEUED", false},
		{"in progress", "IN_PROGRESS", false},
		{"repo migration", "REPO_MIGRATION", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsOrgFailed(tt.state); got != tt.want {
				t.Errorf("IsOrgFailed(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}

func TestIsOrgRepoMigration(t *testing.T) {
	tests := []struct {
		name  string
		state string
		want  bool
	}{
		{"exact match", "REPO_MIGRATION", true},
		{"lowercase", "repo_migration", true},
		{"with whitespace", " REPO_MIGRATION ", true},
		{"other state", "IN_PROGRESS", false},
		{"empty", "", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := migration.IsOrgRepoMigration(tt.state); got != tt.want {
				t.Errorf("IsOrgRepoMigration(%q) = %v, want %v", tt.state, got, tt.want)
			}
		})
	}
}
