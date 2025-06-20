This is a C# based repository that produces several CLIs that are used by customers to interact with the GitHub migration APIs. Please follow these guidelines when contributing:

## Code Standards

### Required Before Each Commit
- Run `dotnet format src/OctoshiftCLI.sln` before committing any changes to ensure proper code formatting. This will run dotnet format on all C# files to maintain consistent style

### Development Flow
- Build: `dotnet build src/OctoshiftCLI.sln /p:TreatWarningsAsErrors=true`
- Test: `dotnet test src/OctoshiftCLI.Tests/OctoshiftCLI.Tests.csproj`

## Repository Structure
- `src/`: Contains the main C# source code for the Octoshift CLI
- `src/ado2gh/`: Contains the ADO to GH CLI commands
- `src/bbs2gh/`: Contains the BBS to GH CLI commands
- `src/gei/`: Contains the GitHub to GitHub CLI commands
- `src/Octoshift/`: Contains shared logic used by multiple commands/CLIs
- `src/OctoshiftCLI.IntegrationTests/`: Contains integration tests for the Octoshift CLI
- `src/OctoshiftCLI.Tests/`: Contains unit tests for the Octoshift CLI

## Key Guidelines
1. Follow C# best practices and idiomatic patterns
2. Maintain existing code structure and organization
4. Write unit tests for new functionality.
5. When making changes that would impact our users (e.g. new features or bug fixes), add a bullet point to `RELEASENOTES.md` with a user friendly brief description of the change
6. Never silently swallow exceptions.
7. If an exception is expected/understood and we can give a helpful user-friendly message, then throw an OctoshiftCliException with a user-friendly message. Otherwise let the exception bubble up and the top-level exception handler will log and handle it appropriately.