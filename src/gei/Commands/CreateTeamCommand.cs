using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class CreateTeamCommand : CreateTeamCommandBase
{
    public CreateTeamCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(log, githubApiFactory)
    {
        Description += Environment.NewLine;
        Description += $"Note: Expects GH_PAT env variable or --{GithubPat.ArgumentHelpName} option to be set.";

        AddOptions();
        Handler = CommandHandler.Create<CreateTeamCommandArgs>(Invoke);
    }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };

    public async Task Invoke(CreateTeamCommandArgs args) =>
        await Handle(args);
}
