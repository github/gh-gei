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

    public async Task Invoke(RevokeMigratorRoleArgs args) => await Handle(args);
}
