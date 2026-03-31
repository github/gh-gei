package github

import "strings"

// AlertStateOpen is the "open" state for alerts.
const AlertStateOpen = "open"

// Repo represents a GitHub repository
type Repo struct {
	Name       string
	Visibility string // "public", "private", "internal"
}

// VersionInfo represents GitHub Enterprise Server version information
type VersionInfo struct {
	Version          string
	InstalledVersion string
}

// Migration represents a GitHub repository migration.
type Migration struct {
	ID              string
	SourceURL       string
	MigrationLogURL string
	State           string
	WarningsCount   int
	FailureReason   string
	RepositoryName  string
	MigrationSource MigrationSource
}

// MigrationSource represents the source of a migration.
type MigrationSource struct {
	ID   string
	Name string
	Type string
}

// OrgMigration represents an organization-level migration.
type OrgMigration struct {
	State                      string
	SourceOrgURL               string
	TargetOrgName              string
	FailureReason              string
	RemainingRepositoriesCount int
	TotalRepositoriesCount     int
}

// Team represents a GitHub team.
type Team struct {
	ID   string
	Name string
	Slug string
}

// Mannequin represents a placeholder user created during migration.
type Mannequin struct {
	ID    string
	Login string
	// MappedUser is the real user this mannequin maps to, if any.
	MappedUser *MannequinUser
}

// MannequinUser represents a real GitHub user that a mannequin maps to.
type MannequinUser struct {
	ID    string
	Login string
}

// MigrationLogResult holds the result of a migration log URL lookup.
type MigrationLogResult struct {
	MigrationLogURL string
	MigrationID     string
}

// CreateAttributionInvitationResult holds the result of creating an attribution invitation.
type CreateAttributionInvitationResult struct {
	Source *Mannequin     `json:"source"`
	Target *MannequinUser `json:"target"`
	Errors []ErrorData    `json:"errors,omitempty"`
}

// ReattributeMannequinToUserResult holds the result of reclaiming a mannequin.
type ReattributeMannequinToUserResult struct {
	Source *Mannequin     `json:"source"`
	Target *MannequinUser `json:"target"`
	Errors []ErrorData    `json:"errors,omitempty"`
}

// ErrorData represents an error from a GraphQL response.
type ErrorData struct {
	Message string `json:"message"`
}

// StartMigrationOption configures optional parameters for StartMigration.
type StartMigrationOption func(*startMigrationParams)

type startMigrationParams struct {
	gitArchiveURL        string
	metadataArchiveURL   string
	skipReleases         bool
	targetRepoVisibility string
	lockSource           bool
}

// WithGitArchiveURL sets the git archive URL for the migration.
func WithGitArchiveURL(u string) StartMigrationOption {
	return func(p *startMigrationParams) {
		p.gitArchiveURL = u
	}
}

// WithMetadataArchiveURL sets the metadata archive URL for the migration.
func WithMetadataArchiveURL(u string) StartMigrationOption {
	return func(p *startMigrationParams) {
		p.metadataArchiveURL = u
	}
}

// WithSkipReleases sets whether to skip releases during migration.
func WithSkipReleases(skip bool) StartMigrationOption {
	return func(p *startMigrationParams) {
		p.skipReleases = skip
	}
}

// WithTargetRepoVisibility sets the target repository visibility.
func WithTargetRepoVisibility(v string) StartMigrationOption {
	return func(p *startMigrationParams) {
		p.targetRepoVisibility = v
	}
}

// WithLockSource sets whether to lock the source repository during migration.
func WithLockSource(lock bool) StartMigrationOption {
	return func(p *startMigrationParams) {
		p.lockSource = lock
	}
}

// ---------------------------------------------------------------------------
// Secret Scanning Alert models
// ---------------------------------------------------------------------------

// SecretScanningAlert represents a secret scanning alert from the GitHub API.
type SecretScanningAlert struct {
	Number            int    `json:"number"`
	State             string `json:"state"`
	Resolution        string `json:"resolution"`
	ResolutionComment string `json:"resolution_comment"`
	SecretType        string `json:"secret_type"`
	Secret            string `json:"secret"`
	ResolverName      string `json:"-"` // populated from resolved_by.login
}

// IsOpen reports whether the alert state is "open".
func (a *SecretScanningAlert) IsOpen() bool {
	return a.State == AlertStateOpen
}

// SecretScanningAlertLocation represents a location where a secret was detected.
// The GitHub API returns {"type": "...", "details": {...}} — we flatten this.
type SecretScanningAlertLocation struct {
	LocationType                string `json:"type"`
	Path                        string `json:"path"`
	StartLine                   int    `json:"start_line"`
	EndLine                     int    `json:"end_line"`
	StartColumn                 int    `json:"start_column"`
	EndColumn                   int    `json:"end_column"`
	BlobSha                     string `json:"blob_sha"`
	IssueTitleUrl               string `json:"issue_title_url"`
	IssueBodyUrl                string `json:"issue_body_url"`
	IssueCommentUrl             string `json:"issue_comment_url"`
	PullRequestTitleUrl         string `json:"pull_request_title_url"`
	PullRequestBodyUrl          string `json:"pull_request_body_url"`
	PullRequestCommentUrl       string `json:"pull_request_comment_url"`
	PullRequestReviewUrl        string `json:"pull_request_review_url"`
	PullRequestReviewCommentUrl string `json:"pull_request_review_comment_url"`
}

// ---------------------------------------------------------------------------
// Code Scanning Alert models
// ---------------------------------------------------------------------------

// CodeScanningAlert represents a code scanning alert.
type CodeScanningAlert struct {
	Number             int                        `json:"number"`
	URL                string                     `json:"url"`
	State              string                     `json:"state"`
	DismissedAt        string                     `json:"dismissed_at"`
	DismissedReason    string                     `json:"dismissed_reason"`
	DismissedComment   string                     `json:"dismissed_comment"`
	RuleId             string                     `json:"-"` // from rule.id
	MostRecentInstance *CodeScanningAlertInstance `json:"-"` // from most_recent_instance
}

// CodeScanningAlertInstance represents an instance of a code scanning alert.
type CodeScanningAlertInstance struct {
	Ref         string `json:"ref"`
	CommitSha   string `json:"commit_sha"`
	Path        string `json:"-"` // from location.path
	StartLine   int    `json:"-"` // from location.start_line
	EndLine     int    `json:"-"` // from location.end_line
	StartColumn int    `json:"-"` // from location.start_column
	EndColumn   int    `json:"-"` // from location.end_column
}

// CodeScanningAnalysis represents a code scanning analysis.
type CodeScanningAnalysis struct {
	ID        int    `json:"id"`
	Ref       string `json:"ref"`
	CommitSha string `json:"commit_sha"`
	CreatedAt string `json:"created_at"`
}

// SarifProcessingStatus represents the status of a SARIF upload.
type SarifProcessingStatus struct {
	Status string   `json:"processing_status"`
	Errors []string `json:"errors"`
}

// IsPending reports whether SARIF processing is still pending.
func (s *SarifProcessingStatus) IsPending() bool {
	return strings.EqualFold(strings.TrimSpace(s.Status), "pending")
}

// IsFailed reports whether SARIF processing has failed.
func (s *SarifProcessingStatus) IsFailed() bool {
	return strings.EqualFold(strings.TrimSpace(s.Status), "failed")
}

// ---------------------------------------------------------------------------
// AutoLink models
// ---------------------------------------------------------------------------

// AutoLink represents an autolink reference configured on a repository.
type AutoLink struct {
	ID          int    `json:"id"`
	KeyPrefix   string `json:"key_prefix"`
	URLTemplate string `json:"url_template"`
}
