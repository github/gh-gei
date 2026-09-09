# gh-gei Go Port — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the gh-gei CLI suite (gei, ado2gh, bbs2gh) from .NET/C# to Go, producing functionally identical binaries with the same CLI interface, output format, and script generation behavior.

**Architecture:** Cobra-based CLIs with explicit dependency wiring in main.go. Consumer-defined interfaces at point of use. GitHub API via go-github + custom GraphQL; ADO/BBS via imroc/req. Side-by-side coexistence with C# code in the same repo.

**Tech Stack:** Go 1.25+, Cobra, go-github/v68, imroc/req/v3, Azure SDK for Go, AWS SDK v2, testify, golangci-lint

**Spec:** `docs/specs/2026-03-30-go-port-design.md`

**Existing work:** PRs #1500 (Phase 1: base framework) and #1501 (Phase 2: gei generate-script)

**PR strategy:** Stack of draft PRs on top of `o1/golang-port/2`. Each phase = 1-2 PRs.

**Local e2e validation:** Focus on macOS. The `GithubToGithub` integration test is the simplest e2e test (only needs `GHEC_PAT`). It exercises `generate-script`, `migrate-repo`, and `download-logs`. After PR 5 is complete, run this test locally as a proof-of-concept. Rely on GitHub CI for Windows and Linux.

---

## Local E2E Setup (Do Once, Before First E2E Validation)

### Task 0: Local e2e infrastructure

Add justfile targets for building, installing, and running Go-based e2e tests locally on macOS.

**Prerequisites:**
- `pwsh` (PowerShell Core) installed
- `gh` CLI installed
- `GHEC_PAT` environment variable set (via direnv `.envrc.local`)
- .NET 8.0 SDK (via mise)

**Files:**
- Modify: `justfile` (add Go extension install + e2e targets)

- [ ] **Step 1: Add `go-install-extensions-macos` justfile target**

```just
# Install Go binaries as gh CLI extensions (macOS)
go-install-extensions-macos: go-publish-macos
    #!/usr/bin/env bash
    set -euo pipefail
    for cli in gei ado2gh bbs2gh; do
        dir="gh-${cli}"
        mkdir -p "$dir"
        cp "./dist/osx-x64/${cli}-darwin-amd64" "./${dir}/gh-${cli}"
        chmod +x "./${dir}/gh-${cli}"
        cd "$dir" && gh extension install . --force && cd ..
    done
    echo "Go extensions installed successfully!"
```

- [ ] **Step 2: Add `go-e2e-github` justfile target**

```just
# Run GithubToGithub integration test against Go binaries (macOS)
go-e2e-github: go-install-extensions-macos
    direnv exec . dotnet test src/OctoshiftCLI.IntegrationTests/OctoshiftCLI.IntegrationTests.csproj \
        --filter "GithubToGithub" \
        --logger "console;verbosity=normal" \
        /p:VersionPrefix=9.9
```

- [ ] **Step 3: Verify infrastructure works with C# binaries first**

Run the C# e2e test to establish a baseline:
```bash
just publish-macos
just install-extensions  # needs updating for macOS
direnv exec . dotnet test src/OctoshiftCLI.IntegrationTests/OctoshiftCLI.IntegrationTests.csproj \
    --filter "GithubToGithub" --logger "console;verbosity=normal" /p:VersionPrefix=9.9
```

This confirms the test infrastructure, credentials, and target orgs work before we try Go binaries.

- [ ] **Step 4: Validate `generate-script` output parity**

After PR 3 tasks are done, use `scripts/validate-scripts.sh` to verify Go `generate-script` output matches C#. This is a lightweight validation that doesn't require real migrations.

### E2E Milestone Checkpoints

| After PR | Validation | What's Tested |
|----------|-----------|---------------|
| PR 3 | `scripts/validate-scripts.sh` | `gei generate-script` output parity |
| PR 5 | `just go-e2e-github` | Full `GithubToGithub` e2e (generate-script + migrate-repo + download-logs) |
| PR 6 | GitHub CI: `AdoBasic`, `AdoCsv` | ADO-to-GitHub migration |
| PR 7 | GitHub CI: `Bbs` | BBS-to-GitHub migration |
| PR 9 | All 15 CI matrix combinations | Full cross-platform e2e |

---

## Chunk 1: Foundation — Shared Commands & GitHub API Client

### PR 3: GitHub GraphQL Client + Shared Low-Complexity Commands

This PR ports the GitHub API surface needed by the shared commands, then implements all 8 shared commands. These are the commands that appear in all three CLIs (gei, ado2gh, bbs2gh) with minimal variation.

**Dependency changes:**
- Add `github.com/google/go-github/v68`
- Add `github.com/shurcooL/graphql` or use custom thin GraphQL client (raw HTTP + JSON, matching GithubClient.cs patterns)

---

### Task 1: Port GithubClient HTTP layer to pkg/github

The C# `GithubClient` handles REST (with Link-header pagination), GraphQL (with cursor pagination), rate limiting, and retry. The Go port splits this: REST goes through `go-github` (which handles pagination and rate limiting natively), while GraphQL uses a thin custom client.

**Files:**
- Create: `pkg/github/graphql.go`
- Create: `pkg/github/graphql_test.go`
- Modify: `pkg/github/client.go` (add go-github integration, remove raw HTTP)
- Modify: `pkg/github/client_test.go`
- Modify: `pkg/github/models.go` (add migration models)
- Create: `pkg/github/ratelimit.go` (secondary rate limit handling)
- Create: `pkg/github/ratelimit_test.go`

**Reference:** `src/Octoshift/Services/GithubClient.cs` (364 lines)

- [ ] **Step 1: Write failing tests for GraphQL client**

Test that the GraphQL client:
- Sends correct `Authorization: Bearer <PAT>` header
- Sends `GraphQL-Features: import_api,mannequin_claiming_emu,org_import_api` header
- Sends `User-Agent: OctoshiftCLI/<version>` header
- Serializes query + variables correctly
- Parses successful responses
- Returns errors from GraphQL `errors` array
- Handles cursor-based pagination (hasNextPage/endCursor)

```go
// pkg/github/graphql_test.go
func TestGraphQLClient_Post(t *testing.T) {
    // httptest server returning canned response
    // verify headers, body, parse response
}

func TestGraphQLClient_Post_WithErrors(t *testing.T) {
    // server returns {"errors": [{"message": "not found"}]}
    // verify error is returned
}

func TestGraphQLClient_PostWithPagination(t *testing.T) {
    // server returns two pages with hasNextPage=true/false
    // verify all results collected
}
```

Run: `go test ./pkg/github/ -run TestGraphQL -v`
Expected: FAIL (graphql.go doesn't exist yet)

- [ ] **Step 2: Implement GraphQL client**

```go
// pkg/github/graphql.go
type graphqlClient struct {
    httpClient *http.Client
    url        string
    headers    map[string]string
    logger     *logger.Logger
}

type graphqlRequest struct {
    Query     string         `json:"query"`
    Variables map[string]any `json:"variables,omitempty"`
}

type graphqlResponse struct {
    Data   json.RawMessage `json:"data"`
    Errors []graphqlError  `json:"errors"`
}

func (c *graphqlClient) Post(ctx context.Context, query string, variables map[string]any) (json.RawMessage, error)
func (c *graphqlClient) PostWithPagination(ctx context.Context, query string, variables map[string]any, dataPath string, pageInfoPath string) ([]json.RawMessage, error)
```

Implement secondary rate limit detection (403/429 with specific messages) with exponential backoff (60s/120s/240s, max 3 retries). Match the C# `GithubClient.HandleSecondaryRateLimitAsync` logic.

Run: `go test ./pkg/github/ -run TestGraphQL -v`
Expected: PASS

- [ ] **Step 3: Write failing tests for go-github REST integration**

Test that the Client:
- Uses go-github for REST operations (repos, teams, orgs)
- Correctly maps go-github types to our domain types
- Handles pagination transparently via go-github's built-in pagination

Run: `go test ./pkg/github/ -run TestClient_GetRepos -v`
Expected: FAIL or needs updating

- [ ] **Step 4: Migrate Client to use go-github for REST**

Replace the raw HTTP calls in `pkg/github/client.go` with `go-github` client calls. The `go-github` library handles pagination, rate limiting, and auth natively.

```go
type Client struct {
    rest    *gogithub.Client  // go-github for REST
    graphql *graphqlClient    // custom for migration GraphQL
    logger  *logger.Logger
    apiURL  string
}

func NewClient(pat string, opts ...Option) (*Client, error)
```

Options: `WithAPIURL(url)`, `WithLogger(log)`, `WithNoSSLVerify()`, `WithUploadsURL(url)`

Run: `go test ./pkg/github/ -v`
Expected: PASS

- [ ] **Step 5: Commit**

---

### Task 2: Port GitHub Migration API Methods

Port the migration-specific methods from `GithubApi.cs` that are needed by the shared commands.

**Files:**
- Modify: `pkg/github/client.go`
- Modify: `pkg/github/client_test.go`
- Modify: `pkg/github/models.go`

**Reference:** `src/Octoshift/Services/GithubApi.cs` — migration, org, mannequin, team, migrator-role sections

- [ ] **Step 1: Write failing tests for organization/user queries**

```go
func TestClient_GetOrganizationId(t *testing.T)      // GraphQL query
func TestClient_GetOrganizationDatabaseId(t *testing.T)
func TestClient_GetEnterpriseId(t *testing.T)
func TestClient_GetLoginName(t *testing.T)           // viewer { login }
func TestClient_GetUserId(t *testing.T)
func TestClient_DoesOrgExist(t *testing.T)           // REST, handle 404
func TestClient_GetOrgMembershipForUser(t *testing.T) // REST, handle 404
```

Run: `go test ./pkg/github/ -run "TestClient_Get(Organization|Enterprise|Login|User|OrgMembership)|TestClient_DoesOrg" -v`
Expected: FAIL

- [ ] **Step 2: Implement organization/user queries**

Map each C# method to its Go equivalent. GraphQL methods use `graphqlClient.Post()`, REST methods use `go-github`.

Run tests. Expected: PASS

- [ ] **Step 3: Write failing tests for migration GraphQL mutations**

```go
func TestClient_CreateAdoMigrationSource(t *testing.T)
func TestClient_CreateBbsMigrationSource(t *testing.T)
func TestClient_CreateGhecMigrationSource(t *testing.T)
func TestClient_StartMigration(t *testing.T)
func TestClient_StartBbsMigration(t *testing.T)
func TestClient_StartOrganizationMigration(t *testing.T)
func TestClient_GetMigration(t *testing.T)
func TestClient_GetOrganizationMigration(t *testing.T)
func TestClient_GetMigrationLogUrl(t *testing.T)
func TestClient_AbortMigration(t *testing.T)
func TestClient_GrantMigratorRole(t *testing.T)
func TestClient_RevokeMigratorRole(t *testing.T)
```

Run: `go test ./pkg/github/ -run "TestClient_(Create.*MigrationSource|Start.*Migration|Get.*Migration|Abort|Grant|Revoke)" -v`
Expected: FAIL

- [ ] **Step 4: Implement migration GraphQL mutations**

Each mutation is a string template + variables. Use `graphqlClient.Post()`. Return parsed IDs/states.

Models needed in `pkg/github/models.go`:
```go
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

type MigrationSource struct { ID, Name, Type string }

type OrgMigration struct {
    State                      string
    SourceOrgURL               string
    TargetOrgName              string
    FailureReason              string
    RemainingRepositoriesCount int
    TotalRepositoriesCount     int
}
```

Run tests. Expected: PASS

- [ ] **Step 5: Write failing tests for team/mannequin REST and GraphQL methods**

```go
func TestClient_CreateTeam(t *testing.T)
func TestClient_GetTeams(t *testing.T)
func TestClient_GetTeamMembers(t *testing.T)
func TestClient_RemoveTeamMember(t *testing.T)
func TestClient_GetTeamSlug(t *testing.T)
func TestClient_AddTeamSync(t *testing.T)
func TestClient_AddTeamToRepo(t *testing.T)
func TestClient_GetIdpGroupId(t *testing.T)
func TestClient_AddEmuGroupToTeam(t *testing.T)
func TestClient_GetMannequins(t *testing.T)
func TestClient_GetMannequinsByLogin(t *testing.T)
func TestClient_CreateAttributionInvitation(t *testing.T)
func TestClient_ReclaimMannequinSkipInvitation(t *testing.T)
```

- [ ] **Step 6: Implement team/mannequin methods**

Run tests. Expected: PASS

- [ ] **Step 7: Commit**

---

### Task 3: Port Shared Command Infrastructure

Port `OctoshiftCliException` equivalent (`UserError`), migration status constants, secret redaction, and the shared command argument validation pattern.

**Files:**
- Create: `internal/cmdutil/errors.go`
- Create: `internal/cmdutil/errors_test.go`
- Create: `internal/cmdutil/flags.go`
- Create: `internal/cmdutil/flags_test.go`
- Create: `pkg/migration/status.go` (migration status constants and helpers)
- Create: `pkg/migration/status_test.go`

**Reference:** `src/Octoshift/OctoshiftCliException.cs`, `src/Octoshift/RepositoryMigrationStatus.cs`, `src/Octoshift/OrganizationMigrationStatus.cs`

- [ ] **Step 1: Write tests for UserError**

```go
func TestUserError_Error(t *testing.T) {
    err := &UserError{Message: "Source org not found"}
    assert.Equal(t, "Source org not found", err.Error())
}

func TestUserError_Unwrap(t *testing.T) {
    inner := errors.New("network failure")
    err := &UserError{Message: "Failed", Err: inner}
    assert.ErrorIs(t, err, inner)
}
```

- [ ] **Step 2: Implement UserError and migration status constants**

```go
// internal/cmdutil/errors.go
type UserError struct {
    Message string
    Err     error
}

// pkg/migration/status.go
const (
    RepoMigrationQueued           = "QUEUED"
    RepoMigrationInProgress       = "IN_PROGRESS"
    RepoMigrationFailed           = "FAILED"
    RepoMigrationSucceeded        = "SUCCEEDED"
    // ...
)

func IsRepoMigrationPending(state string) bool
func IsRepoMigrationSucceeded(state string) bool
func IsRepoMigrationFailed(state string) bool
```

- [ ] **Step 3: Write tests for flag validation helpers**

URL-vs-org detection, mutual exclusivity checks, etc.

- [ ] **Step 4: Implement flag validation helpers**

- [ ] **Step 5: Commit**

---

### Task 4: Port wait-for-migration Command

This is the simplest shared command — a polling loop over the migration status API.

**Files:**
- Create: `cmd/gei/wait_for_migration.go`
- Create: `cmd/gei/wait_for_migration_test.go`

**Reference:** `src/Octoshift/Commands/WaitForMigration/WaitForMigrationCommandHandler.cs` (110 lines)

- [ ] **Step 1: Write failing tests for wait-for-migration**

Table-driven tests:
- Repo migration succeeds immediately
- Repo migration succeeds after 2 polls
- Repo migration fails → error
- Org migration succeeds
- Org migration fails → error
- Invalid migration ID prefix → error
- Missing migration ID → error

```go
// cmd/gei/wait_for_migration_test.go
type mockMigrationWaiter struct {
    mock.Mock
}

func (m *mockMigrationWaiter) GetMigration(ctx context.Context, id string) (*github.Migration, error) {
    args := m.Called(ctx, id)
    return args.Get(0).(*github.Migration), args.Error(1)
}
```

Use consumer-defined interface pattern:
```go
type migrationWaiter interface {
    GetMigration(ctx context.Context, id string) (*github.Migration, error)
    GetOrganizationMigration(ctx context.Context, id string) (*github.OrgMigration, error)
}
```

- [ ] **Step 2: Implement wait-for-migration command**

Poll interval: 60 seconds (make configurable for tests via a field on the command struct or a variable).

```go
func newWaitForMigrationCmd(gh migrationWaiter, log *logger.Logger) *cobra.Command
```

Flags: `--migration-id` (required), `--github-target-pat` (for gei; `--github-pat` for ado2gh/bbs2gh), `--target-api-url`

- [ ] **Step 3: Wire into all 3 CLIs**

Add `newWaitForMigrationCmd(...)` to `cmd/gei/main.go`, `cmd/ado2gh/main.go`, `cmd/bbs2gh/main.go`. Note: in ado2gh/bbs2gh the PAT flag is `--github-pat` not `--github-target-pat`.

- [ ] **Step 4: Run tests**

Run: `go test ./cmd/gei/ -run TestWaitForMigration -v`
Expected: PASS

- [ ] **Step 5: Commit**

---

### Task 5: Port abort-migration Command

**Files:**
- Create: `cmd/gei/abort_migration.go`
- Create: `cmd/gei/abort_migration_test.go`

**Reference:** `src/Octoshift/Commands/AbortMigration/AbortMigrationCommandHandler.cs` (29 lines)

- [ ] **Step 1: Write failing tests**

- Abort succeeds → log success
- Abort fails (returns false) → log error

- [ ] **Step 2: Implement abort-migration**

```go
type migrationAborter interface {
    AbortMigration(ctx context.Context, id string) (bool, error)
}

func newAbortMigrationCmd(gh migrationAborter, log *logger.Logger) *cobra.Command
```

Flags: `--migration-id` (required), `--github-target-pat`/`--github-pat`, `--target-api-url`

- [ ] **Step 3: Wire into all 3 CLIs**

- [ ] **Step 4: Run tests, commit**

---

### Task 6: Port download-logs Command

**Files:**
- Create: `cmd/gei/download_logs.go`
- Create: `cmd/gei/download_logs_test.go`
- Create: `pkg/download/service.go` (HttpDownloadService equivalent)
- Create: `pkg/download/service_test.go`

**Reference:** `src/Octoshift/Commands/DownloadLogs/DownloadLogsCommandHandler.cs` (122 lines), `src/Octoshift/Services/HttpDownloadService.cs` (60 lines)

- [ ] **Step 1: Write tests for download service**

- [ ] **Step 2: Implement download service**

```go
// pkg/download/service.go
type Service struct {
    client *http.Client
    logger *logger.Logger
}

func (s *Service) DownloadToFile(ctx context.Context, url, filepath string) error
func (s *Service) DownloadToBytes(ctx context.Context, url string) ([]byte, error)
```

- [ ] **Step 3: Write tests for download-logs command**

Test both paths:
- By migration ID: calls GetMigration, extracts log URL, downloads
- By org/repo: calls GetMigrationLogUrl, downloads
- File exists without --overwrite → error
- File exists with --overwrite → success
- Migration not found → error

- [ ] **Step 4: Implement download-logs command**

```go
type logDownloader interface {
    GetMigration(ctx context.Context, id string) (*github.Migration, error)
    GetMigrationLogUrl(ctx context.Context, org, repo string) (string, error)
}

func newDownloadLogsCmd(gh logDownloader, dl *download.Service, log *logger.Logger, fs *filesystem.Provider, retry *retry.Policy) *cobra.Command
```

Flags: `--github-target-org`, `--github-target-repo`, `--migration-id`, `--github-target-pat`/`--github-pat`, `--target-api-url`, `--migration-log-file`, `--overwrite`

- [ ] **Step 5: Wire into all 3 CLIs, run tests, commit**

---

### Task 7: Port grant-migrator-role and revoke-migrator-role Commands

**Files:**
- Create: `cmd/gei/grant_migrator_role.go`
- Create: `cmd/gei/grant_migrator_role_test.go`
- Create: `cmd/gei/revoke_migrator_role.go`
- Create: `cmd/gei/revoke_migrator_role_test.go`

**Reference:** `src/Octoshift/Commands/GrantMigratorRole/GrantMigratorRoleCommandHandler.cs`, `src/Octoshift/Commands/RevokeMigratorRole/RevokeMigratorRoleCommandHandler.cs`

- [ ] **Step 1: Write tests for both commands**

Grant: success → log, failure → log error
Revoke: success → log, failure → log error

- [ ] **Step 2: Implement both commands**

```go
type migratorRoleManager interface {
    GetOrganizationId(ctx context.Context, org string) (string, error)
    GrantMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error)
    RevokeMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error)
}
```

Flags: `--github-org` (required), `--actor` (required), `--actor-type` (required, constrained to USER/TEAM), `--github-target-pat`/`--github-pat`, `--target-api-url`

- [ ] **Step 3: Wire into all 3 CLIs, run tests, commit**

---

### Task 8: Port create-team Command

**Files:**
- Create: `cmd/gei/create_team.go`
- Create: `cmd/gei/create_team_test.go`

**Reference:** `src/Octoshift/Commands/CreateTeam/CreateTeamCommandHandler.cs` (83 lines)

- [ ] **Step 1: Write tests**

- Team doesn't exist → create, link IdP group
- Team already exists → log and skip
- IdP group provided → remove members, link group
- No IdP group → skip linking

- [ ] **Step 2: Implement create-team**

```go
type teamCreator interface {
    GetTeams(ctx context.Context, org string) ([]github.Team, error)
    CreateTeam(ctx context.Context, org, name string) (string, string, error)
    GetTeamMembers(ctx context.Context, org, teamSlug string) ([]string, error)
    RemoveTeamMember(ctx context.Context, org, teamSlug, member string) error
    GetIdpGroupId(ctx context.Context, org, groupName string) (int, error)
    AddEmuGroupToTeam(ctx context.Context, org, teamSlug string, groupID int) error
}
```

Flags: `--github-org` (required), `--team-name` (required), `--idp-group`, `--github-target-pat`/`--github-pat`, `--target-api-url`

- [ ] **Step 3: Wire into all 3 CLIs, run tests, commit**

---

### Task 9: Port generate-mannequin-csv and reclaim-mannequin Commands

**Files:**
- Create: `cmd/gei/generate_mannequin_csv.go`
- Create: `cmd/gei/generate_mannequin_csv_test.go`
- Create: `cmd/gei/reclaim_mannequin.go`
- Create: `cmd/gei/reclaim_mannequin_test.go`
- Create: `pkg/mannequin/service.go` (ReclaimService equivalent)
- Create: `pkg/mannequin/service_test.go`

**Reference:** `src/Octoshift/Commands/GenerateMannequinCsv/GenerateMannequinCsvCommandHandler.cs`, `src/Octoshift/Commands/ReclaimMannequin/ReclaimMannequinCommandHandler.cs`, `src/Octoshift/Services/ReclaimService.cs`

- [ ] **Step 1: Write tests for mannequin reclaim service**

Port the core logic from `ReclaimService.cs`: CSV parsing, mannequin matching, invitation vs skip-invitation paths, force mode, duplicate detection.

- [ ] **Step 2: Implement mannequin reclaim service**

- [ ] **Step 3: Write tests for generate-mannequin-csv command**

- [ ] **Step 4: Implement generate-mannequin-csv command**

Flags: `--github-org` (required), `--output`, `--include-reclaimed`, `--github-target-pat`/`--github-pat`, `--target-api-url`

- [ ] **Step 5: Write tests for reclaim-mannequin command**

- [ ] **Step 6: Implement reclaim-mannequin command**

Flags: `--github-org` (required), `--csv`, `--mannequin-user`, `--mannequin-id`, `--target-user`, `--force`, `--skip-invitation`, `--no-prompt`, `--github-target-pat`/`--github-pat`, `--target-api-url`

- [ ] **Step 7: Wire into all 3 CLIs, run tests, commit**

---

### Task 10: Port version checker and GitHub status check

**Files:**
- Create: `pkg/version/checker.go`
- Create: `pkg/version/checker_test.go`
- Create: `pkg/status/github.go`
- Create: `pkg/status/github_test.go`
- Modify: `cmd/gei/main.go` (wire up PersistentPreRunE)
- Modify: `cmd/ado2gh/main.go`
- Modify: `cmd/bbs2gh/main.go`

**Reference:** `src/Octoshift/Services/VersionChecker.cs`, `src/Octoshift/Services/GithubStatusApi.cs`

- [ ] **Step 1: Write tests for version checker**

- Current version < latest → returns false
- Current version == latest → returns true
- Network error → graceful fallback (don't crash)

- [ ] **Step 2: Implement version checker**

```go
// pkg/version/checker.go
type Checker struct {
    httpClient *http.Client
    logger     *logger.Logger
    version    string  // compiled-in version
}

func (c *Checker) IsLatest(ctx context.Context) (bool, error)
func (c *Checker) GetLatestVersion(ctx context.Context) (string, error)
```

Fetch from `https://raw.githubusercontent.com/github/gh-gei/main/LATEST-VERSION.txt`.

- [ ] **Step 3: Write tests for GitHub status API**

- [ ] **Step 4: Implement GitHub status API**

```go
// pkg/status/github.go
func GetUnresolvedIncidentsCount(ctx context.Context) (int, error)
```

- [ ] **Step 5: Wire into root commands' PersistentPreRunE**

Both checks are performed before every command (unless `GEI_SKIP_VERSION_CHECK` / `GEI_SKIP_STATUS_CHECK` env vars are set).

- [ ] **Step 6: Run all tests, run `golangci-lint`, commit**

---

### Task 11: Push PR 3

- [ ] **Step 1: Run full test suite**

```bash
go test -race -count=1 ./...
golangci-lint run
```

- [ ] **Step 2: Push branch and create draft PR**

Base: `o1/golang-port/2`
Title: "Phase 3: GitHub API client + all shared commands (Go port)"

---

## Chunk 2: Core Migration Commands

### PR 4: Cloud Storage Clients

Port Azure Blob, AWS S3, and GitHub-owned storage upload.

---

### Task 12: Port Azure Blob Storage client

**Files:**
- Create: `pkg/storage/azure/client.go`
- Create: `pkg/storage/azure/client_test.go`
- Modify: `go.mod` (add `github.com/Azure/azure-sdk-for-go/sdk/storage/azblob`)

**Reference:** `src/Octoshift/Services/AzureApi.cs` (124 lines)

- [ ] **Step 1: Write tests for Azure client**

- Upload returns SAS URL
- Download returns bytes
- Container naming: `migration-archives-<uuid>`
- Progress logging every 10 seconds

- [ ] **Step 2: Implement Azure client**

```go
type Client struct {
    serviceClient *azblob.Client
    logger        *logger.Logger
}

func NewClient(connectionString string, opts ...Option) (*Client, error)
func (c *Client) Upload(ctx context.Context, fileName string, content io.Reader, size int64) (string, error)
func (c *Client) Download(ctx context.Context, url string) ([]byte, error)
```

Upload creates container `migration-archives-<uuid>`, uploads blob, generates SAS URL (48h expiry, read-only).

- [ ] **Step 3: Run tests, commit**

---

### Task 13: Port AWS S3 client

**Files:**
- Create: `pkg/storage/aws/client.go`
- Create: `pkg/storage/aws/client_test.go`
- Modify: `go.mod` (add `github.com/aws/aws-sdk-go-v2`)

**Reference:** `src/Octoshift/Services/AwsApi.cs` (141 lines)

- [ ] **Step 1: Write tests for AWS client**

- Upload from file path → returns pre-signed URL (48h)
- Upload from stream → returns pre-signed URL
- Progress logging

- [ ] **Step 2: Implement AWS client**

```go
type Client struct {
    s3Client       *s3.Client
    presignClient  *s3.PresignClient
    logger         *logger.Logger
}

func NewClient(ctx context.Context, accessKey, secretKey string, opts ...Option) (*Client, error)
func (c *Client) Upload(ctx context.Context, bucket, key string, data io.Reader) (string, error)
func (c *Client) UploadFile(ctx context.Context, bucket, key, filePath string) (string, error)
```

Options: `WithRegion(r)`, `WithSessionToken(t)`, `WithLogger(l)`

- [ ] **Step 3: Run tests, commit**

---

### Task 14: Port GitHub-owned storage multipart upload

**Files:**
- Create: `pkg/storage/ghowned/client.go`
- Create: `pkg/storage/ghowned/client_test.go`

**Reference:** `src/Octoshift/Services/ArchiveUploader.cs` (190 lines)

- [ ] **Step 1: Write tests for multipart upload**

Test the 3-phase protocol:
- Small archive (< 100 MiB) → single POST
- Large archive → Start (POST) → Parts (PATCH, follow Location header) → Complete (PUT)
- Missing Location header → error
- Custom part size from env var
- Minimum 5 MiB part size enforcement

- [ ] **Step 2: Implement multipart upload**

```go
type Client struct {
    httpClient  *http.Client
    uploadsURL  string
    logger      *logger.Logger
    retryPolicy *retry.Policy
    partSize    int64  // default 100 MiB
}

func NewClient(uploadsURL string, httpClient *http.Client, opts ...Option) *Client
func (c *Client) Upload(ctx context.Context, orgDatabaseID, archiveName string, content io.ReadSeeker, size int64) (string, error)
```

- [ ] **Step 3: Run tests, commit**

---

### Task 15: Port archive upload orchestration

**Files:**
- Create: `pkg/archive/uploader.go`
- Create: `pkg/archive/uploader_test.go`

This coordinates: choose storage backend → upload → return URL.

**Reference:** `MigrateRepoCommandHandler.cs` `UploadArchive()` method pattern

- [ ] **Step 1: Write tests**

- Upload to Azure when Azure configured
- Upload to AWS when AWS configured
- Upload to GitHub-owned when GitHub storage configured
- Error when none configured
- Error when multiple configured

- [ ] **Step 2: Implement orchestration**

- [ ] **Step 3: Run tests, commit**

- [ ] **Step 4: Push PR 4**

Create draft PR. Base: PR 3 branch.
Title: "Phase 4: Cloud storage clients (Azure Blob, AWS S3, GitHub-owned) (Go port)"

---

### PR 5: gei migrate-repo + migrate-org

The most complex commands in the suite.

---

### Task 16: Port gei migrate-repo

**Files:**
- Create: `cmd/gei/migrate_repo.go`
- Create: `cmd/gei/migrate_repo_test.go`

**Reference:** `src/gei/Commands/MigrateRepo/MigrateRepoCommandHandler.cs` (508 lines), `MigrateRepoCommandArgs.cs` (139 lines)

- [ ] **Step 1: Write tests for argument validation**

Port all the cross-field validation from `MigrateRepoCommandArgs.Validate()`:
- Reject URL in org/repo fields
- Default target-repo to source-repo
- Default source PAT to target PAT
- Validate archive URL/path mutual exclusivity
- Validate paired archive options
- AWS bucket requires GHES URL
- no-ssl-verify requires GHES URL
- Azure + GitHub storage conflict

- [ ] **Step 2: Write tests for the happy path flows**

- GitHub.com → GitHub.com (direct, no archive upload)
- GHES → GitHub.com via Azure storage
- GHES → GitHub.com via AWS S3
- GHES → GitHub.com via GitHub-owned storage
- Local archive paths
- Queue-only mode
- Migration failure → error

- [ ] **Step 3: Implement migrate-repo command**

~25 flags. Consumer-defined interfaces for all dependencies.

```go
type migrationRunner interface {
    GetOrganizationId(ctx context.Context, org string) (string, error)
    GetOrganizationDatabaseId(ctx context.Context, org string) (int, error)
    CreateGhecMigrationSource(ctx context.Context, orgID string) (string, error)
    StartMigration(ctx context.Context, opts github.StartMigrationOpts) (string, error)
    GetMigration(ctx context.Context, id string) (*github.Migration, error)
    DoesRepoExist(ctx context.Context, org, repo string) (bool, error)
    // GHES archive methods...
    StartGitArchiveGeneration(ctx context.Context, org string, repos []string) (int, error)
    StartMetadataArchiveGeneration(ctx context.Context, org string, repos []string) (int, error)
    GetArchiveMigrationStatus(ctx context.Context, org string, id int) (string, error)
    GetArchiveMigrationUrl(ctx context.Context, org string, id int) (string, error)
    GetEnterpriseServerVersion(ctx context.Context) (string, error)
    UploadArchiveToGithubStorage(ctx context.Context, orgDBId int, archiveName string, content io.ReadSeeker, size int64) (string, error)
}
```

- [ ] **Step 4: Wire into gei CLI**

- [ ] **Step 5: Run tests, commit**

---

### Task 17: Port GHES version checker

**Files:**
- Create: `pkg/ghes/version.go`
- Create: `pkg/ghes/version_test.go`

**Reference:** `src/gei/Services/GhesVersionChecker.cs`

The GHES version checker determines if blob credentials are required based on the GHES version. Versions < 3.8.0 require Azure/AWS storage; >= 3.8.0 can use GitHub-owned storage.

- [ ] **Step 1: Write tests**
- [ ] **Step 2: Implement**
- [ ] **Step 3: Commit**

---

### Task 18: Port gei migrate-org

**Files:**
- Create: `cmd/gei/migrate_org.go`
- Create: `cmd/gei/migrate_org_test.go`

**Reference:** `src/gei/Commands/MigrateOrg/MigrateOrgCommandHandler.cs`

- [ ] **Step 1: Write tests**
- [ ] **Step 2: Implement**

Flags: `--github-source-org`, `--github-target-org`, `--github-target-enterprise`, `--github-source-pat`, `--github-target-pat`, `--queue-only`, `--target-api-url`

- [ ] **Step 3: Wire into gei CLI, run tests, commit**

---

### Task 19: Port gei migrate-secret-alerts and migrate-code-scanning-alerts

**Files:**
- Create: `pkg/alerts/secret_scanning.go`
- Create: `pkg/alerts/secret_scanning_test.go`
- Create: `pkg/alerts/code_scanning.go`
- Create: `pkg/alerts/code_scanning_test.go`
- Create: `cmd/gei/migrate_secret_alerts.go`
- Create: `cmd/gei/migrate_secret_alerts_test.go`
- Create: `cmd/gei/migrate_code_scanning.go`
- Create: `cmd/gei/migrate_code_scanning_test.go`

**Reference:** `src/Octoshift/Services/SecretScanningAlertService.cs`, `src/Octoshift/Services/CodeScanningAlertService.cs`

Also need to port the GitHub API methods for secret/code scanning (REST endpoints in GithubApi.cs).

- [ ] **Step 1: Add secret scanning REST methods to pkg/github**
- [ ] **Step 2: Add code scanning REST methods to pkg/github**
- [ ] **Step 3: Port SecretScanningAlertService**
- [ ] **Step 4: Port CodeScanningAlertService**
- [ ] **Step 5: Implement migrate-secret-alerts command**
- [ ] **Step 6: Implement migrate-code-scanning-alerts command**
- [ ] **Step 7: Wire into gei CLI, run tests, commit**

- [ ] **Step 8: Push PR 5**

Create draft PR. Base: PR 4 branch.
Title: "Phase 5: gei migrate-repo, migrate-org, alert migration commands (Go port)"

---

## Chunk 3: ADO Client & Commands

### PR 6: ADO API Client + ado2gh Commands

---

### Task 20: Port ADO API client to imroc/req

Replace the existing `pkg/ado/client.go` (which uses `pkg/http`) with `imroc/req`.

**Files:**
- Rewrite: `pkg/ado/client.go`
- Rewrite: `pkg/ado/client_test.go`
- Modify: `pkg/ado/models.go` (add all ADO model types)
- Modify: `go.mod` (add `github.com/imroc/req/v3`)

**Reference:** `src/Octoshift/Services/AdoClient.cs` (241 lines), `src/Octoshift/Services/AdoApi.cs` (889 lines)

The ADO client needs:
- Basic auth (`:pat` base64)
- Continuation token pagination (`x-ms-continuationtoken` header)
- Top/skip pagination (`$top`/`$skip` query params)
- Retry on 503
- Throttling via `Retry-After` header

- [ ] **Step 1: Write tests for ADO client pagination patterns**

Test continuation-token pagination and top/skip pagination.

- [ ] **Step 2: Implement ADO client with imroc/req**

```go
type Client struct {
    http     *req.Client
    baseURL  string
    logger   *logger.Logger
}

func NewClient(baseURL, pat string, opts ...Option) *Client
```

- [ ] **Step 3: Port all ~39 ADO API methods**

Group by area and implement in batches:
1. Org/Identity (GetOrgOwner, GetUserId, GetOrganizations, etc.)
2. Team Projects (GetTeamProjects, GetTeamProjectId)
3. Repos (GetRepos, GetEnabledRepos, GetRepoId, DisableRepo, LockRepo)
4. Pipelines (GetPipelines, GetPipelineId, QueueBuild, GetBuildStatus, etc.)
5. Service Connections (GetGithubAppId, ContainsServiceConnection, ShareServiceConnection)
6. Boards (GetBoardsGithubConnection, CreateBoardsGithubEndpoint, etc.)
7. Git Statistics (GetLastPushDate, GetCommitCountSince, etc.)

Each group gets its own test file or test section.

- [ ] **Step 4: Run tests, commit**

---

### Task 21: Port ado2gh migrate-repo

**Files:**
- Create: `cmd/ado2gh/migrate_repo.go`
- Create: `cmd/ado2gh/migrate_repo_test.go`

**Reference:** `src/ado2gh/Commands/MigrateRepo/MigrateRepoCommandHandler.cs` (105 lines)

Simpler than gei migrate-repo: no archive upload, just creates migration source and starts migration.

- [ ] **Step 1: Write tests**
- [ ] **Step 2: Implement**
- [ ] **Step 3: Wire, run tests, commit**

---

### Task 22: Port ado2gh generate-script

**Files:**
- Create: `cmd/ado2gh/generate_script.go`
- Create: `cmd/ado2gh/generate_script_test.go`
- Create: `pkg/ado/inspector.go` (AdoInspectorService equivalent)
- Create: `pkg/ado/inspector_test.go`

**Reference:** `src/ado2gh/Commands/GenerateScript/GenerateScriptCommandHandler.cs` (459 lines)

This is the ADO variant of generate-script. It:
1. Fetches ADO orgs → projects → repos
2. Optionally loads a CSV repo list
3. Generates PowerShell script with ado2gh-specific commands
4. Supports --all flag for create-teams, lock-repos, disable-repos, rewire-pipelines, etc.

The script generation itself reuses `pkg/scriptgen` (already ported in Phase 2).

- [ ] **Step 1: Port AdoInspectorService**

```go
// pkg/ado/inspector.go
type Inspector struct {
    client     *Client
    logger     *logger.Logger
    orgFilter  string
    projectFilter string
}

func (i *Inspector) GetRepos() (map[string]map[string][]Repository, error)
func (i *Inspector) GetRepoCount() int
```

- [ ] **Step 2: Write tests for generate-script**
- [ ] **Step 3: Implement ado2gh generate-script**
- [ ] **Step 4: Validate with scripts/validate-scripts.sh**
- [ ] **Step 5: Wire, run tests, commit**

---

### Task 23: Port ado2gh simple commands

8 ADO-specific low-complexity commands:

| Command | Handler Lines | Dependencies |
|---------|:---:|---|
| `lock-ado-repo` | 36 | AdoApi |
| `disable-ado-repo` | ~30 | AdoApi |
| `add-team-to-repo` | ~40 | GithubApi |
| `configure-auto-link` | ~50 | GithubApi |
| `share-service-connection` | ~40 | AdoApi |
| `integrate-boards` | ~80 | AdoApi, GithubApi |
| `rewire-pipeline` | ~100 | AdoApi |
| `test-pipelines` | ~100 | AdoApi |

**Files per command:** `cmd/ado2gh/<name>.go` + `cmd/ado2gh/<name>_test.go`

- [ ] **Step 1: Port lock-ado-repo and disable-ado-repo**

These are the simplest — each is a few ADO API calls.

- [ ] **Step 2: Port add-team-to-repo and configure-auto-link**

GitHub API calls via go-github.

- [ ] **Step 3: Port share-service-connection and integrate-boards**

ADO-specific API calls (contribution queries).

- [ ] **Step 4: Port rewire-pipeline**

More complex: fetches pipeline definition, modifies repository configuration, updates.

- [ ] **Step 5: Port test-pipelines**

Concurrent pipeline testing with status polling.

- [ ] **Step 6: Run all tests, lint, commit**

---

### Task 24: Port ado2gh inventory-report

**Files:**
- Create: `cmd/ado2gh/inventory_report.go`
- Create: `cmd/ado2gh/inventory_report_test.go`
- Create: `pkg/ado/csvgen.go` (CSV generator services)
- Create: `pkg/ado/csvgen_test.go`

**Reference:** `src/ado2gh/Commands/InventoryReport/InventoryReportCommandHandler.cs`, `src/ado2gh/Services/OrgsCsvGeneratorService.cs`, `src/ado2gh/Services/TeamProjectsCsvGeneratorService.cs`, `src/ado2gh/Services/ReposCsvGeneratorService.cs`, `src/ado2gh/Services/PipelinesCsvGeneratorService.cs`

- [ ] **Step 1: Port CSV generators**
- [ ] **Step 2: Port inventory-report command**
- [ ] **Step 3: Run tests, commit**

- [ ] **Step 4: Push PR 6**

Create draft PR. Base: PR 5 branch.
Title: "Phase 6: ADO API client + all ado2gh commands (Go port)"

---

## Chunk 4: BBS Client & Commands

### PR 7: BBS API Client + bbs2gh Commands

---

### Task 25: Port BBS API client to imroc/req

Replace the existing `pkg/bbs/client.go` with `imroc/req`.

**Files:**
- Rewrite: `pkg/bbs/client.go`
- Rewrite: `pkg/bbs/client_test.go`
- Modify: `pkg/bbs/models.go`

**Reference:** `src/Octoshift/Services/BbsClient.cs` (116 lines), `src/Octoshift/Services/BbsApi.cs` (148 lines)

BBS pagination: `isLastPage` boolean + `nextPageStart` field + `values[]` array.

- [ ] **Step 1: Write tests for BBS pagination**
- [ ] **Step 2: Implement BBS client with imroc/req**
- [ ] **Step 3: Port all BBS API methods**

Methods:
- GetServerVersion, StartExport, GetExport
- GetProjects, GetProject, GetRepos
- GetIsRepositoryArchived, GetRepositoryPullRequests, GetRepositoryLatestCommitDate, GetRepositoryAndAttachmentsSize

- [ ] **Step 4: Run tests, commit**

---

### Task 26: Port BBS archive downloaders (SSH + SMB)

**Files:**
- Create: `pkg/bbs/ssh_downloader.go`
- Create: `pkg/bbs/ssh_downloader_test.go`
- Create: `pkg/bbs/smb_downloader.go`
- Create: `pkg/bbs/smb_downloader_test.go`
- Modify: `go.mod` (add `golang.org/x/crypto`, evaluate `github.com/hirochachacha/go-smb2`)

**Reference:** `src/bbs2gh/Services/BbsSshArchiveDownloader.cs`, `src/bbs2gh/Services/BbsSmbArchiveDownloader.cs`

- [ ] **Step 1: Port SSH archive downloader**

Uses `golang.org/x/crypto/ssh` to SFTP-download the export archive from BBS.

- [ ] **Step 2: Port SMB archive downloader**

Uses `go-smb2` to download over SMB/CIFS.

- [ ] **Step 3: Write tests, commit**

---

### Task 27: Port bbs2gh migrate-repo

**Files:**
- Create: `cmd/bbs2gh/migrate_repo.go`
- Create: `cmd/bbs2gh/migrate_repo_test.go`

**Reference:** `src/bbs2gh/Commands/MigrateRepo/MigrateRepoCommandHandler.cs` (403 lines)

The most complex BBS command: export generation → archive download (SSH/SMB) → upload (Azure/AWS/GH) → import.

- [ ] **Step 1: Write tests for all 5 phases**
- [ ] **Step 2: Implement bbs2gh migrate-repo**
- [ ] **Step 3: Wire, run tests, commit**

---

### Task 28: Port bbs2gh generate-script

**Files:**
- Create: `cmd/bbs2gh/generate_script.go`
- Create: `cmd/bbs2gh/generate_script_test.go`
- Create: `pkg/bbs/inspector.go`
- Create: `pkg/bbs/inspector_test.go`

**Reference:** `src/bbs2gh/Commands/GenerateScript/GenerateScriptCommandHandler.cs`

- [ ] **Step 1: Port BbsInspectorService**
- [ ] **Step 2: Write tests for generate-script**
- [ ] **Step 3: Implement bbs2gh generate-script**
- [ ] **Step 4: Validate with scripts/validate-scripts.sh**
- [ ] **Step 5: Wire, run tests, commit**

---

### Task 29: Port bbs2gh inventory-report

**Files:**
- Create: `cmd/bbs2gh/inventory_report.go`
- Create: `cmd/bbs2gh/inventory_report_test.go`
- Create: `pkg/bbs/csvgen.go`
- Create: `pkg/bbs/csvgen_test.go`

**Reference:** `src/bbs2gh/Commands/InventoryReport/InventoryReportCommandHandler.cs`, `src/bbs2gh/Services/ProjectsCsvGeneratorService.cs`, `src/bbs2gh/Services/ReposCsvGeneratorService.cs`

- [ ] **Step 1: Port CSV generators**
- [ ] **Step 2: Port inventory-report command**
- [ ] **Step 3: Run tests, commit**

- [ ] **Step 4: Push PR 7**

Create draft PR. Base: PR 6 branch.
Title: "Phase 7: BBS API client + all bbs2gh commands (Go port)"

---

## Chunk 5: CI/CD & E2E Integration

### PR 8: CI/CD Workflow Updates

---

### Task 30: Update CI.yml build job for Go

**Files:**
- Modify: `.github/workflows/CI.yml`

**Reference:** Current CI.yml build job, `justfile` Go targets

Changes to the `build` job:
- Add Go setup step (`actions/setup-go`)
- Add `go-build`, `go-test`, `golangci-lint` steps alongside existing C# steps
- Keep C# steps until the port is complete (both codebases coexist)

- [ ] **Step 1: Add Go build and test to CI build job**
- [ ] **Step 2: Add Go lint step**
- [ ] **Step 3: Test by pushing to PR**

---

### Task 31: Update build-for-e2e-test for Go binaries

**Files:**
- Modify: `.github/workflows/CI.yml` (build-for-e2e-test job)
- Modify: `justfile` (ensure go-publish-* targets produce correct binary names)

The e2e tests expect binaries named `gei-linux-amd64`, `ado2gh-windows-amd64.exe`, etc. The `just go-publish-*` targets must produce cross-compiled binaries with these exact names.

- [ ] **Step 1: Update build-for-e2e-test to use Go cross-compilation**

Replace `dotnet publish` with `GOOS=linux GOARCH=amd64 go build -o dist/gei-linux-amd64 ./cmd/gei` etc.

- [ ] **Step 2: Verify binary artifact naming matches what e2e-test expects**

The e2e-test job downloads artifacts and copies them into the gh extension directory. Binary names must match.

- [ ] **Step 3: Test that e2e-test job can install and run Go binaries**

---

### Task 32: Update publish job for Go binaries

**Files:**
- Create: `publish-go.sh` or modify `publish.ps1` to support Go builds
- Modify: `.github/workflows/CI.yml` (publish job)

Cross-compile for all 6 platform targets:
- `linux-amd64`, `linux-arm64`
- `darwin-amd64`, `darwin-arm64`  
- `windows-amd64`, `windows-386`

- [ ] **Step 1: Create Go publish script**
- [ ] **Step 2: Update publish job to build Go binaries**
- [ ] **Step 3: Update release creation to use Go binaries**

---

### Task 33: Update CodeQL and other CI items

**Files:**
- Modify: `.github/workflows/CI.yml` (CodeQL steps)
- Modify: `.github/codeql/codeql-config.yml`
- Modify: `.github/workflows/copilot-setup-steps.yml`
- Modify: `.github/dependabot.yml` (add `gomod` ecosystem)

- [ ] **Step 1: Add Go language to CodeQL init**
- [ ] **Step 2: Update copilot-setup-steps for Go**
- [ ] **Step 3: Add gomod to dependabot**
- [ ] **Step 4: Run lint, push PR 8**

Create draft PR. Base: PR 7 branch.
Title: "Phase 8: CI/CD workflow updates for Go binaries"

---

### PR 9: E2E Test Validation

This PR ensures all integration tests pass against Go binaries. It should change **only build steps**, not validation logic.

---

### Task 34: Run e2e tests and fix any issues

- [ ] **Step 1: Trigger manual integration test run**

Use the `integration-tests.yml` workflow_dispatch with the Go PR.

- [ ] **Step 2: Analyze failures**

Failures will likely be in:
- Log output format differences (investigate `.octoshift.log` format expectations)
- Binary exit codes (ensure non-zero on error)
- Command output format (ensure exact match)

- [ ] **Step 3: Fix any format discrepancies**

The goal is zero changes to the test validation logic — only the Go code adapts.

- [ ] **Step 4: Re-run e2e tests until all pass**

- [ ] **Step 5: Push PR 9**

Create draft PR. Base: PR 8 branch.
Title: "Phase 9: E2E test compatibility fixes (Go port)"

---

## Chunk 6: Cleanup & Removal

### PR 10: Remove pkg/http and pkg/app

After all consumers are migrated to go-github, imroc/req, and explicit wiring:

**Files:**
- Delete: `pkg/http/client.go`, `pkg/http/client_test.go`
- Delete: `pkg/app/app.go`, `pkg/app/app_test.go`
- Modify: All consumers to remove references

- [ ] **Step 1: Verify no remaining imports of pkg/http or pkg/app**
- [ ] **Step 2: Remove files**
- [ ] **Step 3: Run tests, lint, commit**
- [ ] **Step 4: Push PR 10**

Create draft PR. Base: PR 9 branch.
Title: "Phase 10: Remove deprecated pkg/http and pkg/app packages"

---

## Appendix A: Flag Naming Differences Between CLIs

The same shared command has different flag names across CLIs:

| Shared Flag (C# base) | gei | ado2gh | bbs2gh |
|------------------------|-----|--------|--------|
| `--github-pat` | `--github-target-pat` | `--github-pat` | `--github-pat` |
| `--target-api-url` | `--target-api-url` | `--target-api-url` | `--target-api-url` |

Implementation: Create a shared function returning `*cobra.Command`, then use a wrapper in each CLI's main.go to rename flags:

```go
// Shared implementation
func newWaitForMigrationCmdBase(gh migrationWaiter, log *logger.Logger, patFlagName string) *cobra.Command

// gei wiring
newWaitForMigrationCmdBase(gh, log, "github-target-pat")

// ado2gh/bbs2gh wiring
newWaitForMigrationCmdBase(gh, log, "github-pat")
```

## Appendix B: Interface Evaluation

C# uses many interfaces for DI. The Go port should evaluate each:

| C# Interface | Go Equivalent | Rationale |
|---|---|---|
| `ICommandHandler<T>` | Not needed | Commands are `*cobra.Command` constructors |
| `ISourceGithubApiFactory` | Not needed | Explicit construction in main.go |
| `ITargetGithubApiFactory` | Not needed | Explicit construction in main.go |
| `IVersionProvider` | `version.Checker` (concrete) | No polymorphism needed |
| `IAzureApiFactory` | Not needed | Explicit construction |
| `IBlobServiceClientFactory` | Not needed | Azure SDK handles this |
| `IBbsArchiveDownloader` | Keep as interface | SSH vs SMB runtime dispatch |

Consumer-defined interfaces at each command file provide testability without global interface declarations.

## Appendix C: Estimated Sizes

| PR | Est. Lines | Est. Test Lines | Total |
|---|---:|---:|---:|
| PR 3: GitHub API + shared commands | ~3,000 | ~2,500 | ~5,500 |
| PR 4: Cloud storage clients | ~1,200 | ~800 | ~2,000 |
| PR 5: gei migrate-repo/org + alerts | ~2,500 | ~2,000 | ~4,500 |
| PR 6: ADO client + ado2gh commands | ~3,500 | ~2,500 | ~6,000 |
| PR 7: BBS client + bbs2gh commands | ~2,500 | ~2,000 | ~4,500 |
| PR 8: CI/CD workflows | ~300 | — | ~300 |
| PR 9: E2E fixes | ~200 | — | ~200 |
| PR 10: Cleanup | -400 | -200 | -600 |
| **Total** | **~12,800** | **~9,600** | **~22,400** |

## Appendix D: Dependency Graph

```
PR 3 (shared commands + GitHub API)
  └── PR 4 (cloud storage)
       └── PR 5 (gei migrate-repo/org + alerts)
            └── PR 6 (ADO client + ado2gh)
                 └── PR 7 (BBS client + bbs2gh)
                      └── PR 8 (CI/CD)
                           └── PR 9 (E2E fixes)
                                └── PR 10 (cleanup)
```

Each PR depends on the one above it. They form a linear stack based on `o1/golang-port/2`.
