using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.ConfigureAutoLink
{
    public class ConfigureAutoLinkCommand : CommandBase<ConfigureAutoLinkCommandArgs, ConfigureAutoLinkCommandHandler>
    {
        public ConfigureAutoLinkCommand() : base(
            name: "configure-autolink",
            description: "Configures Autolink References in GitHub so that references to Azure Boards work items become hyperlinks in GitHub" +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
        {
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(GithubPat);
            AddOption(Verbose);
        }

        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true
        };
        public Option<string> GithubRepo { get; } = new("--github-repo")
        {
            IsRequired = true
        };
        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            IsRequired = true
        };
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsRequired = true
        };
        public Option<string> GithubPat { get; } = new("--github-pat");
        public Option<bool> Verbose { get; } = new("--verbose");

        public override ConfigureAutoLinkCommandHandler BuildHandler(ConfigureAutoLinkCommandArgs args, IServiceProvider sp)
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
            var targetGithubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
            var githubApi = targetGithubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

            return new ConfigureAutoLinkCommandHandler(log, githubApi);
        }
    }
}
