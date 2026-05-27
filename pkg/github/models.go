package github

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
