using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.IntegrateBoards
{
    public class IntegrateBoardsCommand : CommandBase<IntegrateBoardsCommandArgs, IntegrateBoardsCommandHandler>
    {
        public IntegrateBoardsCommand() : base(
            name: "integrate-boards",
            description: "Configures the Azure Boards<->GitHub integration in Azure DevOps." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set." +
                         Environment.NewLine +
                         "The ADO_PAT token must have 'All organizations' access selected.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(AdoPat);
            AddOption(GithubPat);
            AddOption(Verbose);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            IsRequired = true
        };
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsRequired = true
        };
        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true
        };
        public Option<string> GithubRepo { get; } = new("--github-repo")
        {
            IsRequired = true
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
        public Option<string> GithubPat { get; } = new("--github-pat");
        public Option<bool> Verbose { get; } = new("--verbose");

        public override IntegrateBoardsCommandHandler BuildHandler(IntegrateBoardsCommandArgs args, IServiceProvider sp)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (sp is null)
            {
                throw new ArgumentNullException(nameof(sp));
            }

            var log = sp.GetRequiredService<OctoLogger>();
            var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var adoApi = adoApiFactory.Create(args.AdoPat);

            return new IntegrateBoardsCommandHandler(log, adoApi, environmentVariableProvider);
        }
    }
}
