# Go Port Development Guide

This document describes the Go implementation of the GitHub Enterprise Importer CLI, which is being ported from C#/.NET to Go.

## Status: Phase 1 Complete ✅

**Phase 1: Foundation** has been completed. The project structure, core packages, and build infrastructure are in place.

### What's Working

- ✅ Go module setup at repo root
- ✅ Directory structure (cmd/, pkg/, internal/)
- ✅ Core packages: logger, retry, env, filesystem
- ✅ Manual DI infrastructure with provider pattern
- ✅ Build system (justfile with Go targets)
- ✅ Linting configuration (golangci-lint)
- ✅ CI workflow for Go
- ✅ Three CLI skeleton binaries (gei, ado2gh, bbs2gh)
- ✅ Comprehensive test suite with 44.9% initial coverage

## Project Structure

```
gh-gei/
├── cmd/                    # CLI entry points
│   ├── gei/               # GitHub to GitHub CLI
│   ├── ado2gh/            # Azure DevOps to GitHub CLI
│   └── bbs2gh/            # Bitbucket to GitHub CLI
├── pkg/                   # Public library code
│   ├── app/              # DI container and app setup
│   ├── logger/           # Structured logging
│   ├── retry/            # Retry logic with exponential backoff
│   ├── env/              # Environment variable access
│   ├── filesystem/       # Filesystem operations
│   ├── models/           # Data models (TBD)
│   └── api/              # API clients (TBD in Phase 2)
│       ├── github/
│       ├── ado/
│       ├── bbs/
│       ├── azure/
│       └── aws/
├── internal/             # Private application code (TBD)
│   ├── gei/
│   ├── ado2gh/
│   └── bbs2gh/
├── src/                  # Existing C# code (will be deleted later)
├── go.mod
├── justfile              # Build tasks
├── .golangci.yml         # Lint configuration
└── .github/workflows/
    ├── CI.yml           # C# CI (existing)
    └── go-ci.yml        # Go CI (new)
```

## Development Workflow

### Building

```bash
# Build all three CLIs
just go-build

# Or manually
go build -o dist/gei ./cmd/gei
go build -o dist/ado2gh ./cmd/ado2gh
go build -o dist/bbs2gh ./cmd/bbs2gh
```

### Testing

```bash
# Run all tests
just go-test

# Run tests with coverage
just go-test-coverage

# Run tests with race detector
go test -race ./...
```

### Linting

```bash
# Run golangci-lint
just go-lint

# Check formatting
just go-format-check

# Auto-format code
just go-format
```

### Cross-Platform Builds

```bash
# Build for Linux
just go-publish-linux

# Build for Windows
just go-publish-windows

# Build for macOS
just go-publish-macos

# Build for all platforms
just go-publish-all
```

## Core Packages

### logger

Provides structured logging with support for different log levels (debug, info, warning, error, verbose). Equivalent to C# `OctoLogger`.

```go
import "github.com/github/gh-gei/pkg/logger"

log := logger.New(verbose)
log.Info("Starting migration for repo %s", repoName)
log.Warning("Rate limit approaching")
log.Error(err)
log.Verbose("Detailed operation info")
```

### retry

Implements retry logic with exponential backoff. Equivalent to C# `RetryPolicy` using Polly.

```go
import "github.com/github/gh-gei/pkg/retry"

policy := retry.New(
    retry.WithMaxAttempts(5),
    retry.WithDelay(1 * time.Second),
)

err := policy.Execute(ctx, func() error {
    return doSomething()
})
```

### env

Provides access to environment variables. Equivalent to C# `EnvironmentVariableProvider`.

```go
import "github.com/github/gh-gei/pkg/env"

envProvider := env.New()
pat := envProvider.TargetGitHubPAT()
skipVersion := envProvider.SkipVersionCheck()
```

### filesystem

Provides filesystem operations. Equivalent to C# `FileSystemProvider`.

```go
import "github.com/github/gh-gei/pkg/filesystem"

fs := filesystem.New()
content, err := fs.ReadAllText("/path/to/file")
err = fs.WriteAllText("/path/to/output", data)
```

### app

Provides manual dependency injection with a provider pattern (compatible with Wire if needed later).

```go
import "github.com/github/gh-gei/pkg/app"

cfg := &app.Config{
    Verbose:       true,
    RetryAttempts: 5,
}

app := app.New(cfg)
app.Logger.Info("Application started")
```

## Design Decisions

### Dependency Injection

We use **manual DI** with a provider pattern. This keeps things simple while maintaining a structure that's compatible with Wire if we need it later.

Provider functions in `pkg/app/app.go` are structured to be Wire-compatible:
```go
func provideLogger(cfg *Config) *logger.Logger { ... }
func provideRetryPolicy(cfg *Config) *retry.Policy { ... }
```

### Command Structure

Using **Cobra** for CLI framework (industry standard, used by `gh` itself):
```go
rootCmd := &cobra.Command{
    Use:   "gei",
    Short: "GitHub Enterprise Importer CLI",
}
rootCmd.AddCommand(newMigrateRepoCmd())
```

### Error Handling

Go-idiomatic error handling with wrapped errors:
```go
if err := validateArgs(args); err != nil {
    return fmt.Errorf("validation failed: %w", err)
}
```

### Testing

Table-driven tests (Go idiom):
```go
tests := []struct {
    name    string
    input   interface{}
    want    interface{}
    wantErr bool
}{
    {"success case", input1, output1, false},
    {"error case", input2, nil, true},
}

for _, tt := range tests {
    t.Run(tt.name, func(t *testing.T) {
        // test implementation
    })
}
```

## CI/CD

### GitHub Actions Workflow

The Go CI workflow (`.github/workflows/go-ci.yml`) runs on every PR and push to main:

1. **Build & Test** (Linux, macOS, Windows)
   - Format checking
   - go vet
   - Build binaries
   - Run tests with race detector
   - Generate coverage reports

2. **Lint** (Linux only)
   - golangci-lint with comprehensive ruleset

### Coexistence with C#

During the transition period:
- C# code stays in `src/`
- Both C# and Go CI workflows run
- Both implementations tested against integration tests
- Go version tagged as "beta" initially

## Next Steps: Phase 2 - API Clients

Phase 2 will implement the API clients:

- [ ] GitHub API client (`pkg/api/github/`)
- [ ] Azure DevOps API client (`pkg/api/ado/`)
- [ ] Bitbucket Server API client (`pkg/api/bbs/`)
- [ ] Azure Blob Storage client (`pkg/api/azure/`)
- [ ] AWS S3 client (`pkg/api/aws/`)
- [ ] HTTP client infrastructure (retry, auth, logging)
- [ ] Unit tests for all API clients

## Code Style

Follow Go best practices:
- Run `gofmt` and `goimports` before committing
- Use table-driven tests
- Prefer explicit error handling over exceptions
- Use `context.Context` for cancellation
- Keep functions focused and testable
- Document public APIs with godoc comments

## Resources

- [Go Documentation](https://go.dev/doc/)
- [Effective Go](https://go.dev/doc/effective_go)
- [Cobra Documentation](https://cobra.dev/)
- [Project Plan](GO_PORT_PLAN.md) (full migration plan)

## Questions?

Refer to the main [CONTRIBUTING.md](CONTRIBUTING.md) for general contribution guidelines, or open a discussion in the GitHub Discussions tab.
