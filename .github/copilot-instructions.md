This is a C# based repository that produces several CLIs that are used by customers to interact with the GitHub migration APIs. A Go port is in progress and coexists with the C# code. Please follow these guidelines when contributing:

## Code Standards

### Required Before Each Commit
- Run `dotnet format src/OctoshiftCLI.sln` before committing any changes to ensure proper code formatting
- Run `go test ./...` if you modify any Go files
- Run `golangci-lint run` if you modify any Go files

### Development Flow
- C# Build: `dotnet build src/OctoshiftCLI.sln /p:TreatWarningsAsErrors=true`
- C# Test: `dotnet test src/OctoshiftCLI.Tests/OctoshiftCLI.Tests.csproj`
- Go Build: `just go-build`
- Go Test: `just go-test`

## Repository Structure
- `src/`: Contains the main C# source code for the Octoshift CLI
- `src/ado2gh/`: Contains the ADO to GH CLI commands
- `src/bbs2gh/`: Contains the BBS to GH CLI commands
- `src/gei/`: Contains the GitHub to GitHub CLI commands
- `src/Octoshift/`: Contains shared logic used by multiple commands/CLIs
- `src/OctoshiftCLI.IntegrationTests/`: Contains integration tests for the Octoshift CLI
- `src/OctoshiftCLI.Tests/`: Contains unit tests for the Octoshift CLI

### Go Port Directories
- `cmd/gei/`, `cmd/ado2gh/`, `cmd/bbs2gh/`: Go CLI entry points
- `pkg/scriptgen/`: PowerShell script generation (ported from C#)
- `pkg/github/`: GitHub API client (REST + GraphQL)
- `pkg/storage/`: Cloud storage clients (Azure Blob, AWS S3, GitHub-owned multipart)
- `pkg/archive/`: Archive upload orchestration
- `pkg/logger/`, `pkg/env/`: Shared Go packages
- `internal/cmdutil/`: Command utility helpers
- `internal/sharedcmd/`: Shared commands (download-logs, version, wait-for-migration, etc.)

## Key Guidelines
1. Follow C# best practices and idiomatic patterns
2. Maintain existing code structure and organization
4. Write unit tests for new functionality.
5. When making changes that would impact our users (e.g. new features or bug fixes), add a bullet point to `RELEASENOTES.md` with a user friendly brief description of the change
6. Never silently swallow exceptions.
7. If an exception is expected/understood and we can give a helpful user-friendly message, then throw an OctoshiftCliException with a user-friendly message. Otherwise let the exception bubble up and the top-level exception handler will log and handle it appropriately.

## Go Port Sync Requirements

**Current state:** The `gei` CLI is fully ported to Go, including `migrate-repo`, `migrate-org`, and all alert migration commands. The GitHub API client, shared commands, and cloud storage clients are also ported.

**When making C# changes, check if the Go port needs updating:**

| C# Area | Go Equivalent | Sync Required? |
|----------|--------------|----------------|
| `src/gei/Commands/` (any command) | `cmd/gei/` | **Yes** — all gei commands are ported |
| `GenerateScriptCommandHandler.cs` (any CLI) | `cmd/{cli}/generate_script.go` + `pkg/scriptgen/generator.go` | **Yes** — scripts must be identical |
| `src/Octoshift/Services/GithubApi.cs` | `pkg/github/client.go` | **Yes** — API behavior must match |
| `src/Octoshift/Services/GithubClient.cs` | `pkg/github/client.go` | **Yes** — HTTP/auth behavior must match |
| Shared commands in `src/Octoshift/Commands/` | `internal/sharedcmd/` | **Yes** — command behavior must match |
| `src/Octoshift/Services/AzureApi.cs` | `pkg/storage/azure/client.go` | **Yes** — upload behavior must match |
| `src/Octoshift/Services/AwsApi.cs` | `pkg/storage/aws/client.go` | **Yes** — upload behavior must match |
| `src/Octoshift/Services/HttpDownloadService.cs` | `pkg/storage/ghowned/client.go` | **Yes** — multipart upload must match |
| `src/Octoshift/Services/ArchiveUploader.cs` | `pkg/archive/uploader.go` | **Yes** — orchestration must match |
| ADO API client (`src/Octoshift/Services/AdoApi.cs`) | Not yet ported | No |
| BBS API client (`src/Octoshift/Services/BbsApi.cs`) | Not yet ported | No |
| `ado2gh` commands | Not yet ported | No |
| `bbs2gh` commands | Not yet ported | No |

**Testing:** Run `go test ./...` to verify Go changes. Run `golangci-lint run` to check for lint issues.
