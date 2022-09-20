using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class RewirePipelineCommand : Command
    {
        public RewirePipelineCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base(
            name: "rewire-pipeline",
            description: "Updates an Azure Pipeline to point to a GitHub repo instead of an Azure Repo." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoPipeline = new Option<string>("--ado-pipeline")
            {
                IsRequired = true,
                Description = "The path and/or name of your pipeline. If the pipeline is in the root pipeline folder this can be just the name. Otherwise you need to specify the full pipeline path (E.g. \\Services\\Finance\\CI-Pipeline)"
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var serviceConnectionId = new Option<string>("--service-connection-id")
            {
                IsRequired = true
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoPipeline);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(serviceConnectionId);
            AddOption(adoPat);
            AddOption(verbose);

            var handler = new RewirePipelineCommandHandler(log, adoApiFactory);
            Handler = CommandHandler.Create<RewirePipelineCommandArgs>(handler.Invoke);
        }
    }

    public class RewirePipelineCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoPipeline { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
