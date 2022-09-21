using System;
using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class GrantMigratorRoleCommand : GrantMigratorRoleCommandBase
{
    public GrantMigratorRoleCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(log, githubApiFactory)
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.ArgumentHelpName} option to be set.";
        
        AddOptions();
        Handler = CommandHandler.Create<GrantMigratorRoleCommandArgs>(BaseHandler.Handle);
    }
}
