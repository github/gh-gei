using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class IntegrateBoardsCommand : Command
    {
        public IntegrateBoardsCommand(OctoLogger log, AdoApiFactory adoApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base(
            name: "integrate-boards",
            description: "Configures the Azure Boards<->GitHub integration in Azure DevOps." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(adoPat);
            AddOption(githubPat);
            AddOption(verbose);

            var handler = new IntegrateBoardsCommandHandler(log, adoApiFactory, environmentVariableProvider);
            Handler = CommandHandler.Create<IntegrateBoardsCommandArgs>(handler.Invoke);
        }
    }

    public class IntegrateBoardsCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string AdoPat { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
