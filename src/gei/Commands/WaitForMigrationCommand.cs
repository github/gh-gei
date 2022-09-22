using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class WaitForMigrationCommand : WaitForMigrationCommandBase
{
    public WaitForMigrationCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base(log, targetGithubApiFactory)
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.ArgumentHelpName} option to be set.";

        AddOptions();
        Handler = CommandHandler.Create<WaitForMigrationCommandArgs>(Invoke);
    }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };

    internal async Task Invoke(WaitForMigrationCommandArgs args) => await Handle(new OctoshiftCLI.Commands.WaitForMigrationCommandArgs
    {
        MigrationId = args.MigrationId,
        GithubPat = args.GithubTargetPat,
        Verbose = args.Verbose
    });
}

public class WaitForMigrationCommandArgs
{
    public string MigrationId { get; set; }
    public string GithubTargetPat { get; set; }
    public bool Verbose { get; set; }
}
