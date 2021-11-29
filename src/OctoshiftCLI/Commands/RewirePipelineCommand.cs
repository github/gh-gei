using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class RewirePipelineCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoFactory;

        public RewirePipelineCommand(OctoLogger log, AdoApiFactory adoFactory) : base("rewire-pipeline")
        {
            _log = log;
            _adoFactory = adoFactory;

            Description = "Updates an Azure Pipeline to point to a GitHub repo instead of an Azure Repo.";

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

            using var ado = _adoFactory.Create();

            var adoPipelineId = await ado.GetPipelineId(adoOrg, adoTeamProject, adoPipeline);
            var pipelineDetails = await ado.GetPipeline(adoOrg, adoTeamProject, adoPipelineId);
            await ado.ChangePipelineRepo(pipelineDetails, githubOrg, githubRepo, serviceConnectionId);

            _log.LogSuccess("Successfully rewired pipeline");
        }
    }
}