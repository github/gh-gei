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
            description: "Configures the Azure Boards<->GitHub integration in Azure DevOps using a GitHub App service connection." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set." +
                         Environment.NewLine +
                         "Requires a pre-configured GitHub App service connection. Use --service-connection-id to specify the service connection," +
                         " or the command will attempt to find one that matches the GitHub org name.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(ServiceConnectionId);
            AddOption(AdoPat);
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
        public Option<string> ServiceConnectionId { get; } = new("--service-connection-id")
        {
            Description = "The ID of the GitHub App service connection to use. If not provided, will attempt to find one matching the GitHub org name."
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
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
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var adoApi = adoApiFactory.Create(args.AdoPat);

            return new IntegrateBoardsCommandHandler(log, adoApi);
        }
    }
}
