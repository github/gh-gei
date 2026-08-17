# gh-gei Go Port — Design Specification

**Date:** 2026-03-30
**Status:** Draft
**Author:** offbyone + AI assistant

## Overview

Port the gh-gei CLI suite (gei, ado2gh, bbs2gh) from .NET 8 / C# 12 to Go, producing functionally identical binaries with the same command-line interface, output format, and script generation behavior. The Go and C# codebases coexist in the same repository during the transition.

## Goals

1. **Binary compatibility**: Same binary names (`gei`, `ado2gh`, `bbs2gh`), same subcommands, same flags, same output
2. **Script compatibility**: Generated PowerShell scripts must be identical (they invoke the CLI as `gh gei`, etc.)
3. **Test compatibility**: Existing e2e tests pass against Go binaries (only build steps change, not validation)
4. **Log compatibility**: Same log file format (`.octoshift.log`, `.octoshift.verbose.log`) so e2e log assertions pass
   - this is the loosest of the goals; if the log assertions assume C# traces, then the e2e test may need to be amended.
5. **Coexistence**: Both codebases live in the same repo for side-by-side inspection
6. **Idiomatic Go**: Use Go conventions (consumer-defined interfaces, explicit wiring, stdlib patterns) rather than mirroring C# architecture
7. **Replacement**: When e2e compatibility is achieved, this will completely replace the C# version

## Non-Goals

- Changing the user-facing CLI interface
- Changing what the generated scripts do
- Rewriting the e2e test harness in Go (deferred to a later phase)
- Supporting new features not in the C# version

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| CLI framework | Cobra | Already established in Phase 1/2, de facto Go standard, same as `gh` CLI |
| GitHub API | `google/go-github` + custom GraphQL | go-github covers REST; migration APIs need custom GraphQL |
| ADO/BBS HTTP | `imroc/req` | Batteries-included: retry, middleware, JSON marshaling |
| Cloud storage | Official AWS SDK v2 + Azure SDK for Go | 1:1 match with C# SDKs, well-maintained |
| Testing | `testify/assert` + `httptest` + table-driven | Already established in Phase 1/2 |
| Linting | `golangci-lint` (25+ linters) | Already configured in Phase 1 |
| Interfaces | Consumer-defined | Idiomatic Go; interfaces declared where used, not where implemented |
| DI | Explicit wiring in main.go | No container; Cobra command constructors accept dependencies |
| Coexistence | Side-by-side at repo root | Go at `cmd/`, `pkg/`, `go.mod`; C# stays in `src/` |
| Integration tests | Hybrid: keep C# harness initially, port to Go later | C# tests are black-box (shell out to `gh` extensions), work against any binary |

## Architecture

### Package Layout

```
cmd/
  gei/
    main.go                    # Root command, subcommand registration
    migrate_repo.go            # migrate-repo command
    migrate_org.go             # migrate-org command
    generate_script.go         # generate-script command (exists in Phase 2)
    wait_for_migration.go      # wait-for-migration command
    abort_migration.go         # abort-migration command
    download_logs.go           # download-logs command
    create_team.go             # create-team command
    grant_migrator_role.go     # grant-migrator-role command
    revoke_migrator_role.go    # revoke-migrator-role command
    reclaim_mannequin.go       # reclaim-mannequin command
    generate_mannequin_csv.go  # generate-mannequin-csv command
    migrate_secret_alerts.go   # migrate-secret-alerts command
    migrate_code_scanning.go   # migrate-code-scanning-alerts command
  ado2gh/
    main.go
    migrate_repo.go
    generate_script.go
    # ... 19 commands total (see command inventory below)
  bbs2gh/
    main.go
    migrate_repo.go
    generate_script.go
    inventory_report.go
    # ... 11 commands total

pkg/
  github/
    client.go                  # GitHub API client (REST via go-github + GraphQL)
    client_test.go
    graphql.go                 # GraphQL mutation/query helpers
    graphql_test.go
    models.go                  # GitHub-specific types
  ado/
    client.go                  # ADO API client (imroc/req)
    client_test.go
    models.go
  bbs/
    client.go                  # BBS API client (imroc/req)
    client_test.go
    models.go
  storage/
    azure/
      client.go               # Azure Blob Storage operations
      client_test.go
    aws/
      client.go               # AWS S3 operations
      client_test.go
    ghowned/
      client.go               # GitHub-owned storage multipart upload
      client_test.go
  scriptgen/
    generator.go               # Script generation engine (exists in Phase 2)
    generator_test.go
    templates.go               # PowerShell template constants
  logger/
    logger.go                  # OctoLogger equivalent (exists in Phase 1)
    logger_test.go
  env/
    env.go                     # Environment variable provider (exists in Phase 1)
    env_test.go
  retry/
    retry.go                   # Retry policy (exists in Phase 1)
    retry_test.go
  filesystem/
    filesystem.go              # File system operations (exists in Phase 1)
    filesystem_test.go
  version/
    checker.go                 # CLI version checking against latest release
    checker_test.go
  status/
    github.go                  # githubstatus.com API
    github_test.go
  confirmation/
    prompt.go                  # Interactive Y/N confirmation
    prompt_test.go
  archive/
    uploader.go                # Archive upload orchestration (Azure/AWS/GH-owned)
    uploader_test.go
  http/
    client.go                  # Shared HTTP client (Phase 1; stdlib + retry)
    client_test.go
  app/
    app.go                     # Centralized DI provider struct (Phase 1)
    app_test.go

internal/
  cmdutil/
    flags.go                   # Shared flag definitions and validation helpers
    errors.go                  # OctoshiftCliError equivalent
  testutil/
    httpmock.go                # Shared HTTP test helpers
    fixtures.go                # Test data loading
```

**Migration path for Phase 1/2 packages:**

- **`pkg/http`** — A thin stdlib wrapper providing retry, TLS config, and basic GET/POST/PUT/DELETE. The GitHub client will migrate to `google/go-github` (which manages its own HTTP transport). ADO and BBS clients will migrate to `imroc/req`, which supersedes `pkg/http` with built-in retry, middleware, and JSON marshaling. `pkg/http` will be removed once all consumers are migrated.
- **`pkg/app`** — A centralized DI provider struct (`App` with `Logger`, `Env`, `FileSystem`, `Retry` fields). The target architecture replaces this with explicit wiring in each `cmd/*/main.go`: command constructors accept their dependencies directly, and `pkg/app` is removed. No Wire or container — just constructor calls.

### Command Pattern

Each command is a function returning `*cobra.Command`. Dependencies are injected via function parameters. Interfaces are defined at the point of consumption.

```go
// cmd/gei/migrate_repo.go

// Only the methods this command actually calls
type migrationStarter interface {
    StartMigration(ctx context.Context, opts github.MigrateOpts) (string, error)
    GetMigrationState(ctx context.Context, migrationID string) (string, error)
}

type migrationLogger interface {
    GetMigrationLogURL(ctx context.Context, org, migrationID string) (string, error)
}

func newMigrateRepoCmd(gh migrationStarter, logs migrationLogger, log *logger.Logger, env *env.Env) *cobra.Command {
    var opts struct {
        sourceOrg    string
        sourceRepo   string
        targetOrg    string
        targetRepo   string
        targetPAT    string
        // ... all flags
    }

    cmd := &cobra.Command{
        Use:   "migrate-repo",
        Short: "Migrate a repository",
        RunE: func(cmd *cobra.Command, args []string) error {
            ctx := cmd.Context()
            // validation, business logic, API calls
            migrationID, err := gh.StartMigration(ctx, github.MigrateOpts{...})
            if err != nil {
                return fmt.Errorf("starting migration: %w", err)
            }
            // poll for completion, download logs on failure, etc.
            return nil
        },
    }

    cmd.Flags().StringVar(&opts.sourceOrg, "github-source-org", "", "...")
    // ... register all flags
    return cmd
}
```

### CLI Wiring (main.go)

> **Refactor note:** Phase 1/2 used context-value injection for passing dependencies to command handlers (e.g., `context.WithValue(ctx, "logger", log)` in `PersistentPreRun`, retrieved via `cmd.Context().Value("logger")`). The target architecture replaces this with explicit constructor injection — each command constructor accepts its dependencies as typed parameters — which is more type-safe and testable. The `getLogger(cmd)` / `getEnvProvider()` helper pattern will be removed.

```go
// cmd/gei/main.go
func main() {
    log := logger.New(os.Stdout, os.Stderr)
    envProvider := env.New()
    
    root := &cobra.Command{
        Use:     "gei",
        Short:   "GitHub Enterprise Importer",
        Version: version,
    }
    root.PersistentFlags().BoolVar(&verbose, "verbose", false, "...")

    // Wire dependencies and register commands
    ghClient := github.NewClient(envProvider.TargetGithubPAT(), github.WithLogger(log))
    
    root.AddCommand(
        newMigrateRepoCmd(ghClient, ghClient, log, envProvider),
        newGenerateScriptCmd(ghClient, log, envProvider),
        newWaitForMigrationCmd(ghClient, log),
        // ... all commands
    )

    if err := root.Execute(); err != nil {
        os.Exit(1)
    }
}
```

### API Client Design

**GitHub (`pkg/github`):**

```go
type Client struct {
    rest    *gogithub.Client    // google/go-github for REST
    graphql *graphqlClient      // custom thin wrapper for migration mutations
    logger  *logger.Logger
}

// REST operations (delegated to go-github)
func (c *Client) GetRepos(ctx context.Context, org string) ([]Repo, error)
func (c *Client) GetTeamMembers(ctx context.Context, org, team string) ([]string, error)

// GraphQL operations (custom, migration-specific)
func (c *Client) CreateMigrationSource(ctx context.Context, opts MigrationSourceOpts) (string, error)
func (c *Client) StartRepositoryMigration(ctx context.Context, opts MigrateOpts) (string, error)
func (c *Client) GetMigrationState(ctx context.Context, migrationID string) (string, error)
func (c *Client) StartOrganizationMigration(ctx context.Context, opts OrgMigrateOpts) (string, error)
```

**ADO (`pkg/ado`):**

```go
type Client struct {
    http   *req.Client
    pat    string
    logger *logger.Logger
}

func NewClient(baseURL, pat string, opts ...Option) *Client
func (c *Client) GetTeamProjects(ctx context.Context, org string) ([]TeamProject, error)
func (c *Client) GetRepos(ctx context.Context, org, project string) ([]Repo, error)
func (c *Client) DisableRepo(ctx context.Context, org, project, repoID string) error
func (c *Client) LockRepo(ctx context.Context, org, project, repoID string) error
// ... ~20 methods mapping to ADO REST API endpoints
```

**BBS (`pkg/bbs`):**

```go
type Client struct {
    http     *req.Client
    username string
    password string
    logger   *logger.Logger
}

func NewClient(baseURL, username, password string, opts ...Option) *Client
func (c *Client) GetProjects(ctx context.Context) ([]Project, error)
func (c *Client) GetRepos(ctx context.Context, projectKey string) ([]Repo, error)
func (c *Client) GetArchive(ctx context.Context, projectKey, repoSlug string) (io.ReadCloser, error)
```

### Migration from Phase 1/2

The Phase 1/2 code established initial implementations with direct parameter injection. The target architecture differs in several ways:

- **Client constructors:** Phase 1/2 clients (e.g., `github.NewClient(cfg, httpClient, log)`, `ado.NewClient(baseURL, pat, log, httpClient)`) accept dependencies as positional parameters. The target architecture uses functional options (e.g., `github.NewClient(pat, github.WithLogger(log))`) for cleaner extensibility. Phase 3+ work will refactor existing clients to match.
- **HTTP layer:** Phase 1/2 clients depend on `pkg/http.Client` (a thin stdlib wrapper). The target architecture has `pkg/github` using `google/go-github` and `pkg/ado` / `pkg/bbs` using `imroc/req`. The `pkg/http` package will be removed once migration is complete.
- **DI pattern:** Phase 1/2 uses `pkg/app.App` as a centralized provider struct. The target architecture wires dependencies explicitly in `main.go` via constructor injection — no central container.
- **Consumer-defined interfaces:** Phase 1/2 code does not yet define interfaces at the consumer site. The target architecture declares narrow interfaces in each command file (e.g., `migrationStarter` in `migrate_repo.go`), enabling easy mocking and testability.

### Error Handling

The C# codebase uses `OctoshiftCliException` for user-friendly errors vs. letting unexpected exceptions bubble up. In Go:

```go
// internal/cmdutil/errors.go
type UserError struct {
    Message string
    Err     error
}

func (e *UserError) Error() string { return e.Message }
func (e *UserError) Unwrap() error { return e.Err }

// Usage: return &cmdutil.UserError{Message: "Source org not found. Check the --github-source-org value."}
```

The root command's `PersistentPreRunE` or a wrapper handles the distinction: `UserError` gets a clean message; other errors get full stack trace in verbose mode.

### Logging

Port `OctoLogger` behavior to `pkg/logger`:

- **Console output**: Info (stdout), Warning (yellow, stderr), Error (red, stderr)
- **File output**: `.octoshift.log` (info+), `.octoshift.verbose.log` (all including debug)
- **Secret redaction**: `logger.RegisterSecret(secret)` — all output is scrubbed
- **Warning counter**: `logger.Warnings()` returns count for summary output
- **Verbose mode**: Controlled by `--verbose` flag, enables debug output to console

### Script Generation

The `pkg/scriptgen` package (Phase 2) generates PowerShell scripts. The generated scripts:

1. Define helper functions (`Exec`, `ExecAndGetMigrationID`)
2. Validate required environment variables
3. Generate `gh gei migrate-repo` / `gh ado2gh migrate-repo` / `gh bbs2gh migrate-repo` calls
4. In parallel mode: queue migrations with `--queue-only`, collect IDs, then `wait-for-migration` for each
5. In sequential mode: run each migration synchronously via `Exec`
6. Print summary (success/failure counts)

Scripts must produce byte-identical output to the C# version. The validation script (`scripts/validate-scripts.sh`) diffs C# vs Go output.

### Cloud Storage

**Azure Blob (`pkg/storage/azure`):**

```go
type Client struct {
    serviceClient *azblob.Client
    logger        *logger.Logger
}

func NewClient(connectionString string, opts ...Option) (*Client, error)
func (c *Client) Upload(ctx context.Context, container, blob string, data io.Reader) (string, error)
func (c *Client) Download(ctx context.Context, container, blob string) (io.ReadCloser, error)
```

**AWS S3 (`pkg/storage/aws`):**

```go
type Client struct {
    s3Client *s3.Client
    logger   *logger.Logger
}

func NewClient(ctx context.Context, accessKey, secretKey, region string, opts ...Option) (*Client, error)
func (c *Client) Upload(ctx context.Context, bucket, key string, data io.Reader) (string, error)
```

**GitHub-owned storage (`pkg/storage/ghowned`):**

```go
type Client struct {
    httpClient *http.Client
    logger     *logger.Logger
}

func (c *Client) Upload(ctx context.Context, uploadURL string, data io.Reader, partSizeMiB int) error
```

### CI/CD Changes

**Build workflow changes:**
- Replace `setup-dotnet` with `actions/setup-go`
- Replace `dotnet build` with `go build ./cmd/...`
- Replace `dotnet test` with `go test -race -coverprofile=... ./...`
- Replace `dotnet publish` with cross-compiled `GOOS=X GOARCH=Y go build` 
- Replace `dotnet format --verify-no-changes` with `golangci-lint run`
- Update CodeQL from `csharp` to `go`

**E2e workflow changes (build steps only):**
- `build-for-e2e-test` job: replace `dotnet publish` with `go build` cross-compilation
- Binary naming convention already matches Go convention: `gei-linux-amd64`, `gei-darwin-arm64`, etc.
- Keep `dotnet test` for integration test runner (C# harness stays)

**Validation steps unchanged:**
- Binary download/copy/chmod
- `gh extension install`
- Integration test execution (still C# xunit runner)
- Log file collection and assertion
- Test result publishing

## Command Inventory

### gei (13 commands)

| Command | Shared? | Complexity |
|---------|---------|------------|
| `migrate-repo` | No | High — orchestrates migration source creation, migration start, archive upload, polling |
| `migrate-org` | No | High — organization-level migration |
| `generate-script` | No | Medium — Phase 2 started this |
| `wait-for-migration` | Yes | Low — poll migration state |
| `abort-migration` | Yes | Low — single API call |
| `download-logs` | Yes | Low — fetch log URL, download |
| `create-team` | Yes | Low — create team + set IdP |
| `grant-migrator-role` | Yes | Low — single GraphQL mutation |
| `revoke-migrator-role` | Yes | Low — single GraphQL mutation |
| `reclaim-mannequin` | Yes | Medium — mannequin reclaim logic |
| `generate-mannequin-csv` | Yes | Medium — fetch + format mannequins |
| `migrate-secret-alerts` | No | Medium — paginate + migrate alerts |
| `migrate-code-scanning-alerts` | No | Medium — paginate + migrate alerts |

### ado2gh (19 commands)

| Command | Shared? | Complexity |
|---------|---------|------------|
| `migrate-repo` | No | High |
| `generate-script` | No | Medium |
| `inventory-report` | No | Medium — fetch all orgs/projects/repos, generate CSV |
| `rewire-pipeline` | No | Medium — update pipeline service connections |
| `test-pipelines` | No | Medium — concurrent pipeline testing |
| `add-team-to-repo` | No | Low |
| `configure-auto-link` | No | Low |
| `disable-repo` | No | Low |
| `integrate-boards` | No | Low |
| `lock-repo` | No | Low |
| `share-service-connection` | No | Low |
| `wait-for-migration` | Yes | Low |
| `abort-migration` | Yes | Low |
| `download-logs` | Yes | Low |
| `create-team` | Yes | Low |
| `grant-migrator-role` | Yes | Low |
| `revoke-migrator-role` | Yes | Low |
| `reclaim-mannequin` | Yes | Medium |
| `generate-mannequin-csv` | Yes | Medium |

### bbs2gh (11 commands)

| Command | Shared? | Complexity |
|---------|---------|------------|
| `migrate-repo` | No | High — includes SSH/SMB archive download |
| `generate-script` | No | Medium |
| `inventory-report` | No | Medium |
| `wait-for-migration` | Yes | Low |
| `abort-migration` | Yes | Low |
| `download-logs` | Yes | Low |
| `create-team` | Yes | Low |
| `grant-migrator-role` | Yes | Low |
| `revoke-migrator-role` | Yes | Low |
| `reclaim-mannequin` | Yes | Medium |
| `generate-mannequin-csv` | Yes | Medium |

## Phased Delivery Plan

### Phase 3: Complete generate-script (PR #3 on stack)
- `gei generate-script` already exists (Phase 2); no work needed for gei
- Wire `generate-script` into ado2gh and bbs2gh CLIs (ADO/BBS-specific variants)
- Validate with `scripts/validate-scripts.sh`
- ~500 lines

### Phase 4: Core migration commands (PR #4-5)
- `migrate-repo` for all 3 CLIs (highest complexity)
- `wait-for-migration`, `abort-migration`, `download-logs` (shared)
- GitHub GraphQL client for migration APIs
- ~3,000 lines

### Phase 5: Cloud storage clients (PR #6)
- Azure Blob, AWS S3, GitHub-owned storage upload
- Archive upload orchestration
- ~1,500 lines

### Phase 6: ADO-specific commands (PR #7)
- 8 ADO-only commands (lock-repo, disable-repo, rewire-pipeline, etc.)
- `inventory-report` for ado2gh and bbs2gh
- `test-pipelines`
- ~2,000 lines

### Phase 7: Remaining commands (PR #8)
- `migrate-org` (gei only)
- Mannequin commands (reclaim, generate-csv)
- Team/role commands (create-team, grant/revoke-migrator-role)
- Alert migration commands (secret-alerts, code-scanning)
- ~2,500 lines

### Phase 8: CI/CD integration (PR #9)
- Update `CI.yml` build steps for Go
- Update `build-for-e2e-test` for Go cross-compilation
- Update `publish` job for Go binaries
- Keep C# integration test runner
- Update `copilot-setup-steps.yml`
- ~200 lines of workflow YAML

### Phase 9: Port integration tests to Go (PR #10+)
- Rewrite `OctoshiftCLI.IntegrationTests` in Go
- Remove C# dependency from e2e workflow
- This can be a separate project after the main port

## Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Script output divergence | `scripts/validate-scripts.sh` runs in CI, diffs C# vs Go output |
| GraphQL API compatibility | Test against real GitHub API; migration mutations are documented |
| BBS SSH/SMB archive download | Go has `golang.org/x/crypto/ssh` for SSH; evaluate `hirochachacha/go-smb2` for SMB |
| go-github missing methods | go-github is comprehensive; for gaps, use raw HTTP via its `Client.NewRequest` |
| E2e test flakiness during transition | Run both C# and Go builds in CI, compare results |
| Performance differences | Go binaries will likely be faster; ensure no timeout assumptions in tests |

## Dependencies

```
# Already in use (Phase 1/2)
github.com/spf13/cobra                     # CLI framework
github.com/stretchr/testify                # Test assertions
github.com/avast/retry-go/v4              # Retry with backoff (used by pkg/retry)

# To be added
github.com/google/go-github/v68            # GitHub REST API
github.com/imroc/req/v3                    # HTTP client for ADO/BBS
github.com/aws/aws-sdk-go-v2              # AWS S3
github.com/Azure/azure-sdk-for-go         # Azure Blob Storage
golang.org/x/crypto                        # SSH client (for BBS)
github.com/hirochachacha/go-smb2          # SMB client (for BBS, needs evaluation)
```
