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
        Handler = CommandHandler.Create<string, string, bool>(Invoke);
    }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };

    public async Task Invoke(string migrationId, string githubTargetPat = null, bool verbose = false) => await Handle(migrationId, githubTargetPat, verbose);
}
