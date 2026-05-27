package app

import (
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/filesystem"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/retry"
)

// App contains all the dependencies for the CLI application
// This provides manual dependency injection with a provider pattern
type App struct {
	Logger     *logger.Logger
	Env        *env.Provider
	FileSystem *filesystem.Provider
	Retry      *retry.Policy
	// API clients will be added in Phase 2
	// GitHubClient  *github.Client
	// ADOClient     *ado.Client
	// BBSClient     *bbs.Client
	// AzureClient   *azure.Client
	// AWSClient     *aws.Client
}

// Config contains configuration for initializing the app
type Config struct {
	Verbose       bool
	LogFile       string
	RetryAttempts uint
}

// New creates a new App with all dependencies initialized
// This is the main provider function for dependency injection
func New(cfg *Config) *App {
	// Initialize core dependencies
	log := provideLogger(cfg)
	envProvider := provideEnvProvider()
	fsProvider := provideFileSystemProvider()
	retryPolicy := provideRetryPolicy(cfg)

	return &App{
		Logger:     log,
		Env:        envProvider,
		FileSystem: fsProvider,
		Retry:      retryPolicy,
	}
}

// Provider functions - these are structured to be compatible with Wire
// if we decide to adopt it later

func provideLogger(cfg *Config) *logger.Logger {
	// TODO: Handle log file if specified
	return logger.New(cfg.Verbose)
}

func provideEnvProvider() *env.Provider {
	return env.New()
}

func provideFileSystemProvider() *filesystem.Provider {
	return filesystem.New()
}

func provideRetryPolicy(cfg *Config) *retry.Policy {
	attempts := cfg.RetryAttempts
	if attempts == 0 {
		attempts = 3 // default
	}
	return retry.New(retry.WithMaxAttempts(attempts))
}

// Additional provider functions will be added as we implement API clients:
// - provideGitHubClient
// - provideADOClient
// - provideBBSClient
// - provideAzureClient
// - provideAWSClient
