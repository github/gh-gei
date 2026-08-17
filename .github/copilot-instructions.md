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
- `pkg/logger/`, `pkg/env/`: Shared Go packages
- `internal/cmdutil/`: Command utility helpers

## Key Guidelines
1. Follow C# best practices and idiomatic patterns
2. Maintain existing code structure and organization
4. Write unit tests for new functionality.
5. When making changes that would impact our users (e.g. new features or bug fixes), add a bullet point to `RELEASENOTES.md` with a user friendly brief description of the change
6. Never silently swallow exceptions.
7. If an exception is expected/understood and we can give a helpful user-friendly message, then throw an OctoshiftCliException with a user-friendly message. Otherwise let the exception bubble up and the top-level exception handler will log and handle it appropriately.

## Go Port Sync Requirements

**Current state:** The Go port has the base framework and `generate-script` commands for all three CLIs. Script generation has full behavioral parity with C#.

**When making C# changes to script generation logic:**
- If you modify `GenerateScriptCommandHandler.cs` in any of the three CLIs, you MUST make the corresponding change in Go:
  - `src/gei/Commands/GenerateScript/` → `cmd/gei/generate_script.go` + `pkg/scriptgen/generator.go`
  - `src/ado2gh/Commands/GenerateScript/` → `cmd/ado2gh/generate_script.go`
  - `src/bbs2gh/Commands/GenerateScript/` → `cmd/bbs2gh/generate_script.go`
- Run `go test ./...` to verify the Go changes compile and tests pass
- Generated PowerShell scripts must be identical between C# and Go

**When making other C# changes:** No Go sync required yet. The remaining commands are not yet ported.
