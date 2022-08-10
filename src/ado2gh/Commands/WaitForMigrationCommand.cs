using System;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class WaitForMigrationCommand : WaitForMigrationCommandBase
{
    public WaitForMigrationCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(log, githubApiFactory)
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.ArgumentHelpName} option to be set.";

        AddOptions();
        Handler = CommandHandler.Create<string, string, bool>(Invoke);
    }

    public async Task Invoke(string migrationId, string githubPat = null, bool verbose = false) => await Handle(migrationId, githubPat, verbose);
}
