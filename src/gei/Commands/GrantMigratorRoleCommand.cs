using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class GrantMigratorRoleCommand : GrantMigratorRoleCommandBase
{
    public GrantMigratorRoleCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(log, githubApiFactory)
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.ArgumentHelpName} option to be set.";

        AddOptions();
        Handler = CommandHandler.Create<GrantMigratorRoleCommandArgs>(Invoke);
    }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };

    internal async Task Invoke(GrantMigratorRoleCommandArgs args) =>
        await BaseHandler.Handle(new OctoshiftCLI.Commands.GrantMigratorRoleCommandArgs
        {
            GithubOrg = args.GithubOrg,
            Actor = args.Actor,
            ActorType = args.ActorType,
            GithubPat = args.GithubTargetPat,
            Verbose = args.Verbose,
            GhesApiUrl = args.GhesApiUrl
        });
}

public class GrantMigratorRoleCommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubTargetPat { get; set; }
    public bool Verbose { get; set; }
    public string GhesApiUrl { get; set; }
}
