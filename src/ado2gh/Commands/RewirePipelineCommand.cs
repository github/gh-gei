using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class RewirePipelineCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly Lazy<AdoApi> _lazyAdoApi;

        public RewirePipelineCommand(OctoLogger log, Lazy<AdoApi> lazyAdoApi) : base("rewire-pipeline")
        {
            _log = log;
            _lazyAdoApi = lazyAdoApi;

            Description = "Updates an Azure Pipeline to point to a GitHub repo instead of an Azure Repo.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT env variable to be set.";

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
            var serviceConnectionId = new Option<string>("--service-connection-id")
            {
                IsRequired = true
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoPipeline);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(serviceConnectionId);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string serviceConnectionId, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation($"Rewiring Pipeline to GitHub repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO PIPELINE: {adoPipeline}");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            _log.LogInformation($"SERVICE CONNECTION ID: {serviceConnectionId}");

            var ado = _lazyAdoApi.Value;

            var adoPipelineId = await ado.GetPipelineId(adoOrg, adoTeamProject, adoPipeline);
            var (defaultBranch, clean, checkoutSubmodules) = await ado.GetPipeline(adoOrg, adoTeamProject, adoPipelineId);
            await ado.ChangePipelineRepo(adoOrg, adoTeamProject, adoPipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, serviceConnectionId);

            _log.LogSuccess("Successfully rewired pipeline");
        }
    }
}