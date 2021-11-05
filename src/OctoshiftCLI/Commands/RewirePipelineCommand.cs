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

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoPipeline);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(serviceConnectionId);

            Handler = CommandHandler.Create<string, string, string, string, string, string>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string serviceConnectionId)
        {
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