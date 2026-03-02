package env

import "os"

// Provider provides access to environment variables
// Equivalent to C# EnvironmentVariableProvider
type Provider struct{}

// New creates a new environment variable provider
func New() *Provider {
	return &Provider{}
}

// SourceGitHubPAT returns the GH_SOURCE_PAT environment variable
func (p *Provider) SourceGitHubPAT() string {
	return os.Getenv("GH_SOURCE_PAT")
}

// TargetGitHubPAT returns the GH_PAT environment variable
func (p *Provider) TargetGitHubPAT() string {
	return os.Getenv("GH_PAT")
}

// ADO_PAT returns the ADO_PAT environment variable
func (p *Provider) ADOPAT() string {
	return os.Getenv("ADO_PAT")
}

// BBSUsername returns the BBS_USERNAME environment variable
func (p *Provider) BBSUsername() string {
	return os.Getenv("BBS_USERNAME")
}

// BBSPassword returns the BBS_PASSWORD environment variable
func (p *Provider) BBSPassword() string {
	return os.Getenv("BBS_PASSWORD")
}

// AzureStorageConnectionString returns the AZURE_STORAGE_CONNECTION_STRING environment variable
func (p *Provider) AzureStorageConnectionString(required bool) string {
	value := os.Getenv("AZURE_STORAGE_CONNECTION_STRING")
	if required && value == "" {
		// In Go, we'll handle this at the caller level
		// Just return empty string here
	}
	return value
}

// SkipVersionCheck returns the GEI_SKIP_VERSION_CHECK environment variable
func (p *Provider) SkipVersionCheck() string {
	return os.Getenv("GEI_SKIP_VERSION_CHECK")
}

// SkipStatusCheck returns the GEI_SKIP_STATUS_CHECK environment variable
func (p *Provider) SkipStatusCheck() string {
	return os.Getenv("GEI_SKIP_STATUS_CHECK")
}

// AWSAccessKeyID returns the AWS_ACCESS_KEY_ID environment variable
func (p *Provider) AWSAccessKeyID() string {
	return os.Getenv("AWS_ACCESS_KEY_ID")
}

// AWSSecretAccessKey returns the AWS_SECRET_ACCESS_KEY environment variable
func (p *Provider) AWSSecretAccessKey() string {
	return os.Getenv("AWS_SECRET_ACCESS_KEY")
}

// AWSSessionToken returns the AWS_SESSION_TOKEN environment variable
func (p *Provider) AWSSessionToken() string {
	return os.Getenv("AWS_SESSION_TOKEN")
}

// AWSRegion returns the AWS_REGION environment variable
func (p *Provider) AWSRegion() string {
	return os.Getenv("AWS_REGION")
}

// AWSBucketName returns the AWS_BUCKET_NAME environment variable
func (p *Provider) AWSBucketName() string {
	return os.Getenv("AWS_BUCKET_NAME")
}

// GitHubOwnedStorageMultipartMebibytes returns the GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES environment variable
func (p *Provider) GitHubOwnedStorageMultipartMebibytes() string {
	return os.Getenv("GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES")
}

// GetOrDefault returns the environment variable value or a default
func (p *Provider) GetOrDefault(key, defaultValue string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return defaultValue
}

// Set sets an environment variable (useful for testing)
func (p *Provider) Set(key, value string) error {
	return os.Setenv(key, value)
}
