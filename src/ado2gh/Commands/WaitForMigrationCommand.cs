using System;
using System.CommandLine.NamingConventionBinder;
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
        Handler = CommandHandler.Create<WaitForMigrationCommandArgs>(BaseHandler.Handle);
    }
}
