## Contributing

Hi there! We're thrilled that you'd like to contribute to this project. Your help is essential for keeping it great.

Contributions to this project are [released](https://docs.github.com/site-policy/github-terms/github-terms-of-service#6-contributions-under-repository-license) to the public under the [project's open source license](LICENSE.md).

Please note that this project is released with a [Contributor Code of Conduct](CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

Here's some helpful notes on how to contribute to this project, including details on how to get started working the codebase.

## How to submit a bug or request a feature

If you think you've found a bug or have a great idea for new functionality please create an Issue in this repo. We have Issue Templates for both Bugs and Feature Requests.

## How to provide feedback or ask for help

Use the [Discussions](https://github.com/github/gh-gei/discussions) tab in this repo for more general feedback or any questions/comments on this tooling.

## Product Backlog

All work done by the maintainers of this repo is tracked in this repo using Issues. We have a hierarchical backlog with Epics at the top, broken down into Batches then broken down to Tasks (epic/batch/task is indicated via labels on the issues). You can see an example Epic and navigate down from there [here](https://github.com/github/gh-gei/issues/101).

## Running tests

### In the terminal
If you want to run tests selectively in the terminal, you can use dotnet test with `--filter` option.

Here are some examples:
1. Run all tests in `AdoApiTests` class. Navigate to either `src` (where the `sln` file is) or `src/octoshiftcli.tests` (where the `csproj` file is) and then execute the following command:
 ```
 dotnet test --filter AdoApiTests
 ```

2. Run a specific test-`GetUserId_Should_Return_UserId` in `src/OctoshiftCLI.Tests/Octoshift/Services/AdoApiTests`. Navigate to `src` or `src/octoshiftcli.tests` and then execute the following command:
```
dotnet test --filter AdoApiTests.GetUserId_Should_Return_UserId
```

### Debugger

If you are using VS code, you can install the [C# dev kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) which will add the testing extension.
- Press the play to run the entire test suite, or navigate to the specific test you would like to run.
- If you set a breakpoint within your code and press the play button with the bug next to it you will be able to inspect your code in more detial.

### Useful links
1. [Dotnet test commands](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test#filter-option-details)
2. [Run selective unit tests](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=mstest)
3. [C# dev kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)



## Debugging code

There are two ways to debug code within VS code.

### Run GH-GEI command locally
Run the following commands in your terminal depending on the provider you are looking to run the commands for.

Azure DevOps:
- Generic: `dotnet run --project src/ado2gh/ado2gh.csproj -- [command]`
- Example: `dotnet run --project src/ado2gh/ado2gh.csproj -- migrate-repo --help`

Bitbucket Server:
- Generic: `dotnet run --project src/bbs2gh/bbs2gh.csproj -- [command]`
- Example: `dotnet run --project src/bbs2gh/bbs2gh.csproj -- migrate-repo --help`

GitHub:
- Generic: `dotnet run --project src/gei/gei.csproj -- [command]`
- Example: `dotnet run --project src/gei/gei.csproj -- migrate-repo --help`

### Run code in a C# REPL

You can use a C# REPL to execute any C# code. There are many C# REPLs available, CSharpRepl is one of them and can be installed globally with the following:

In your terminal:
`dotnet tool install --global CSharpRepl  --version [version]`
(v0.4.0 is the latest version compatible with .NET 6.0)

Run it:
`csharprepl -r src/bbs2gh/bin/Debug/net8.0/Octoshift.dll`

Then load up assemblies:
```csharp
#r "Octoshift.dll"

// You might need others, for example the AWS SDK:
#r "AWSSDK.Core.dll"
#r "AWSSDK.S3.dll"

// Add necessary usings
using OctoshiftCLI.Services;

// Instantiate your classes
var aws = new AwsApi("access-key-id", "secret-access-key");
```

### Use debugger

If you use the built in debugger you are able to set breakpoints and inspect the code within VS Code.

1. Navigate to `.vs_code/launch.json`.
2. Find the command you are looking to run, for example: `Launch ado2gh`.
3. Update the args property to have the arguments you're looking to test (using real org and project names):
    - "args": ["migrate-repo", "--ado-org", "example-org", "--ado-team-project", "example-project" ...]
4. Set a breakpoint within your code as needed.
5. Navigate to the `Run and debug` side panel option.
6. Navigate to the drop down menu and select the command you would like to run, for example: `Launch ado2gh`.
7. Press the play button

### Useful links

1. [Run a .NET app](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-run)
2. [Debugging using vscode](https://code.visualstudio.com/docs/editor/debugging)

## Submitting a Pull Request

Before submitting a Pull Request please first open an issue to get feedback on the change you intend to submit.

When creating a PR the template will prompt you to confirm that you have done a few required steps (or at least considered them and determined they are not necessary on this PR):

1. Most code should include unit tests (and sometimes e2e tests). New features should include new tests in the same PR. And changes to existing behaviour should update the relevant tests.

2. If this change is something that users should be notified about (e.g. most bug fixes and new features - but probably not code refactorings) be sure to add one or more bullets to the `RELEASENOTES.md` file. The contents of this file will automatically be included in the next release.

3. Consider whether the code changes should have any additional (or changed) log output and be sure those logging changes are included in the same PR.

4. Most PR's should be linked to one or more relevant issues that they implement.

For more info on how to get started contributing code see [this doc](docs/ContributingCode.md).
