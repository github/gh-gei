using System.CommandLine;
using System.Runtime.CompilerServices;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands;

public class WaitForMigrationCommandBase : Command
{
    protected WaitForMigrationCommandBaseHandler BaseHandler { get; init; }

    public WaitForMigrationCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "wait-for-migration",
        description: "Waits for migration(s) to finish and reports all in progress and queued ones.")
    {
        BaseHandler = new WaitForMigrationCommandBaseHandler(log, githubApiFactory);
    }

    protected virtual Option<string> MigrationId { get; } = new("--migration-id")
    {
        IsRequired = true,
        Description = "Waits for the specified migration to finish."
    };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat") { IsRequired = false };

    protected virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

    protected void AddOptions()
    {
        AddOption(MigrationId);
        AddOption(GithubPat);
        AddOption(Verbose);
    }
}

public class WaitForMigrationCommandArgs
{
    public string MigrationId { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
