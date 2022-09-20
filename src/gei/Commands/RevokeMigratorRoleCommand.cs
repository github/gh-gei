using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class RevokeMigratorRoleCommand : RevokeMigratorRoleCommandBase
{
    public RevokeMigratorRoleCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(log, githubApiFactory)
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.GetLogFriendlyName()} option to be set.";

        AddOptions();
        Handler = CommandHandler.Create<RevokeMigratorRoleArgs>(Invoke);
    }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };

    internal async Task Invoke(RevokeMigratorRoleArgs args) => await Handle(new OctoshiftCLI.Commands.RevokeMigratorRoleArgs
    {
        GithubOrg = args.GithubOrg,
        Actor = args.Actor,
        ActorType = args.ActorType,
        GithubPat = args.GithubTargetPat,
        Verbose = args.Verbose
    });
}

public class RevokeMigratorRoleArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubTargetPat { get; set; }
    public bool Verbose { get; set; }
}
