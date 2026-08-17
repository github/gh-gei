# Go Port Development Guide

This document describes the Go implementation of the GitHub Enterprise Importer CLI, which is being ported from C#/.NET to Go.

## Status: Phase 2 In Progress 🚧

**Phase 1: Foundation** ✅ Complete  
**Phase 2: API Clients + Script Generation** 🚧 In Progress (80% complete)

### Phase 1 Complete ✅

- ✅ Go module setup at repo root
- ✅ Directory structure (cmd/, pkg/, internal/)
- ✅ Core packages: logger, retry, env, filesystem, app
- ✅ Manual DI infrastructure with provider pattern
- ✅ Build system (justfile with Go targets)
- ✅ Linting configuration (golangci-lint)
- ✅ CI workflow for Go
- ✅ Three CLI skeleton binaries (gei, ado2gh, bbs2gh)
- ✅ Comprehensive test suite with 44.9% initial coverage

### Phase 2 Progress (80% Complete)

**Completed:**
- ✅ **pkg/http** - Shared HTTP client with retry logic (75.5% coverage)
  - GET/POST/PUT/DELETE methods with headers support
  - Automatic retry with exponential backoff
  - SSL verification bypass option
  - Context-aware requests
  - JSON payload support
  
- ✅ **pkg/github** - GitHub API client (93.9% coverage)
  - `GetRepos(ctx, org)` - Fetch all org repositories with pagination
  - `GetVersion(ctx)` - GHES version checking
  - Automatic pagination (100 items per page)
  - URL encoding for org names
  - Bearer token authentication

- ✅ **pkg/ado** - Azure DevOps API client (88.0% coverage)
  - `GetTeamProjects(ctx, org)` - Fetch all team projects
  - `GetRepos(ctx, org, teamProject)` - Fetch all repos in a project
  - `GetEnabledRepos(ctx, org, teamProject)` - Filter enabled repos
  - `GetGithubAppId(ctx, org, githubOrg, teamProjects)` - Find GitHub App service connection
  - Basic auth with PAT token
  - URL encoding and proper error handling

- ✅ **pkg/bbs** - Bitbucket Server API client (91.1% coverage)
  - `GetProjects(ctx)` - Fetch all projects with automatic pagination
  - `GetRepos(ctx, projectKey)` - Fetch all repos with automatic pagination
  - Basic auth with username/password
  - Handles BBS pagination model (nextPageStart)
  - URL encoding for project keys

**In Progress:**
- 🚧 **pkg/scriptgen** - PowerShell script generation

**Remaining Phase 2 Work:**
- [ ] Create PowerShell script generation package with Go templates
- [ ] Add comprehensive tests for script generation (85%+ coverage)
- [ ] Add script validation tool to compare C# vs Go outputs
- [ ] Document script generation templates and validation process

## Project Structure

```
gh-gei/
├── cmd/                    # CLI entry points
│   ├── gei/               # GitHub to GitHub CLI
│   ├── ado2gh/            # Azure DevOps to GitHub CLI
│   └── bbs2gh/            # Bitbucket to GitHub CLI
├── pkg/                   # Public library code
│   ├── app/              # DI container and app setup (100.0% coverage)
│   ├── logger/           # Structured logging (76.9% coverage)
│   ├── retry/            # Retry logic with exponential backoff (96.2% coverage)
│   ├── env/              # Environment variable access
│   ├── filesystem/       # Filesystem operations
│   ├── http/             # Shared HTTP client (75.5% coverage) ✅
│   ├── github/           # GitHub API client (93.9% coverage) ✅
│   ├── ado/              # Azure DevOps API client (88.0% coverage) ✅
│   ├── bbs/              # Bitbucket Server API client (91.1% coverage) ✅
│   └── scriptgen/        # PowerShell script generation 🚧
├── testdata/             # Test fixtures and sample data
│   ├── github/           # GitHub API test fixtures
│   ├── ado/              # Azure DevOps API test fixtures
│   └── bbs/              # Bitbucket Server API test fixtures
├── scripts/              # Utility scripts
│   └── validate-scripts.sh  # Compare C# vs Go PowerShell outputs
├── internal/             # Private application code (TBD Phase 3)
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

# Run specific package tests
go test ./pkg/github/... -v
go test ./pkg/http/... -v

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

### http

Shared HTTP client with built-in retry logic, SSL verification bypass, and context support.

```go
import "github.com/github/gh-gei/pkg/http"

httpClient := http.NewClient(http.Config{
    Timeout:       30 * time.Second,
    RetryAttempts: 3,
    NoSSLVerify:   false,
}, log)

body, err := httpClient.Get(ctx, url, headers)
```

### github

GitHub API client for interacting with GitHub.com and GitHub Enterprise Server.

```go
import "github.com/github/gh-gei/pkg/github"

client := github.NewClient(github.Config{
    APIURL: "https://api.github.com",
    PAT:    "ghp_...",
}, httpClient, log)

repos, err := client.GetRepos(ctx, "my-org")
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

## Migration Plan Overview

### Phase 1: Foundation ✅ (Complete)
- Go module setup
- Core packages (logger, retry, env, filesystem, app)
- Build infrastructure
- CI/CD setup
- Test framework

### Phase 2: API Clients + Script Generation 🚧 (In Progress - 80% Complete)

**Completed:**
- ✅ HTTP client infrastructure (75.5% coverage)
- ✅ GitHub API client (93.9% coverage)
- ✅ Azure DevOps API client (88.0% coverage)
- ✅ Bitbucket Server API client (91.1% coverage)

**In Progress:**
- 🚧 PowerShell script generation package

**Key Features:**
- ✅ RESTful API clients for GitHub, ADO, and BBS
- ✅ Automatic pagination support (all APIs)
- ✅ Authentication (Bearer tokens for GitHub, Basic auth for ADO/BBS)
- ✅ Retry logic with exponential backoff (integrated)
- ✅ Context-aware operations with cancellation support
- ✅ URL encoding and proper error handling
- 🚧 PowerShell script generation using Go text/template
- ✅ Comprehensive unit tests (80%+ coverage achieved for all API clients)
- 🚧 Script validation tool for C# vs Go output comparison

### Phase 3: Commands Implementation (Planned - 3-4 weeks)

**Priority Order:**
1. **`generate-script`** command (all 3 CLIs) - Week 1-2
   - Primary usage model: users generate scripts first
   - Requires: API clients + script generation package
   - Output: PowerShell scripts for migration workflows

2. **`migrate-repo`** command (all 3 CLIs) - Week 2-3
   - Most complex command
   - Requires: Archive creation, blob storage upload, migration API

3. **`wait-for-migration`** command (all 3 CLIs) - Week 3
   - Poll migration status with exponential backoff

4. **`download-logs`** command (GEI, ADO2GH) - Week 4
   - Fetch and save migration logs

5. **Additional commands** as needed

### Phase 4: Storage & Advanced Features (Planned - 2-3 weeks)
- Azure Blob Storage client
- AWS S3 client
- Archive creation and upload
- Multipart upload support
- Remaining commands (lock-repo, disable-repo, etc.)

### Phase 5: Integration & Polish (Planned - 2 weeks)
- Integration tests comparing C# vs Go outputs
- Performance benchmarking
- Documentation updates
- Beta release preparation

## Important Note: GitHub API Client Strategy

**UPDATE:** The `gh` CLI provides a mature, well-tested API client library via `github.com/cli/go-gh/v2/pkg/api`.

**Plan Update:**
- Phase 2: Keep current custom GitHub client for basic operations (already 93.9% complete)
- Phase 3: During command implementation, evaluate switching to `go-gh/v2/pkg/api` for:
  - Authentication handling (already integrated with gh credentials)
  - GraphQL support (if needed)
  - Better GitHub.com API compatibility
  - Built-in rate limiting and retry logic

**Benefits of go-gh API client:**
- Reuses existing `gh` authentication
- Battle-tested by GitHub CLI team
- Handles pagination, rate limiting, and retries
- GraphQL and REST support
- Better integration with GitHub ecosystem

**Decision Point:** After Phase 2 completes, we'll evaluate:
1. Keep custom client (simpler, already working)
2. Switch to go-gh (better long-term, more features)
3. Hybrid approach (go-gh for complex operations, custom for simple ones)

## Script Generation Feature (Critical Path)

The primary usage model for GEI is:
1. User runs `generate-script` command
2. CLI generates a PowerShell script (`migrate.ps1`)
3. User reviews/modifies the script
4. User executes the script, which calls the CLI repeatedly

**Script Types:**
- **Sequential**: Commands execute one-by-one, each waits for completion
- **Parallel** (default): Queues all migrations, then waits for all to complete

**Script Structure:**
```powershell
#!/usr/bin/env pwsh
# Version comment
# Helper functions (Exec, ExecAndGetMigrationID)
# Environment variable validation
# Migration commands (or queue + wait)
# Summary report (parallel only)
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
- Integration tests compare C# vs Go script outputs

## Code Style

Follow Go best practices:
- Run `gofmt` and `goimports` before committing
- Use table-driven tests
- Prefer explicit error handling over exceptions
- Use `context.Context` for cancellation
- Keep functions focused and testable
- Document public APIs with godoc comments
- Use `testdata/` for test fixtures

## Test Coverage Goals

- **Phase 1**: ✅ 44.9% initial coverage achieved
- **Phase 2**: ✅ 80%+ achieved for all API client packages
  - pkg/app: ✅ 100.0%
  - pkg/http: ✅ 75.5%
  - pkg/github: ✅ 93.9%
  - pkg/ado: ✅ 88.0%
  - pkg/bbs: ✅ 91.1%
  - pkg/logger: ✅ 76.9%
  - pkg/retry: ✅ 96.2%
  - pkg/scriptgen: 🚧 Target 85%+
- **Phase 3**: Maintain 75%+ overall coverage
- **Phase 4**: Maintain 75%+ overall coverage

**Current Overall Coverage:** ~85% (packages with tests)

## Resources

- [Go Documentation](https://go.dev/doc/)
- [Effective Go](https://go.dev/doc/effective_go)
- [Cobra Documentation](https://cobra.dev/)
- [go-gh API Client](https://github.com/cli/go-gh)
- [C# Source Code](src/) - Reference implementation
- [CONTRIBUTING.md](CONTRIBUTING.md) - General contribution guidelines

## Current Sprint: Phase 2 Completion

**This Week's Goals:**
1. ✅ Complete pkg/http with tests (75.5% coverage)
2. ✅ Complete pkg/github with tests (93.9% coverage)
3. ✅ Complete pkg/ado with tests (88.0% coverage)
4. ✅ Complete pkg/bbs with tests (91.1% coverage)
5. 🚧 Complete pkg/scriptgen with tests (target 85%+ coverage)
6. 🚧 Add script validation tool

**Next Week's Goals (Phase 3 Start):**
1. Implement `generate-script` command for GEI
2. Implement `generate-script` command for ADO2GH
3. Implement `generate-script` command for BBS2GH
4. Add integration tests comparing C# vs Go script outputs
5. Validate script equivalence in CI

## Script Validation

To ensure the Go port produces equivalent PowerShell scripts to the C# version, we've added a validation mechanism:

### Manual Validation

```bash
# Generate scripts with both C# and Go versions
dotnet run --project src/gei/gei.csproj -- generate-script --args... > csharp-script.ps1
./dist/gei generate-script --args... > go-script.ps1

# Compare (ignoring version comments)
diff -u --ignore-matching-lines="^# Generated by" csharp-script.ps1 go-script.ps1
```

### Automated CI Validation

The CI workflow will automatically:
1. Build both C# and Go versions
2. Run `generate-script` with identical inputs
3. Compare outputs (ignoring version metadata)
4. Fail if scripts differ semantically

Script validation tests are located in:
- `scripts/validate-scripts.sh` - Bash script for comparison
- `.github/workflows/validate-scripts.yml` - CI integration

## Questions?

Refer to the main [CONTRIBUTING.md](CONTRIBUTING.md) for general contribution guidelines, or open a discussion in the GitHub Discussions tab.
