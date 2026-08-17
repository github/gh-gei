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
