using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class DisableRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApi _adoApi;

        public DisableRepoCommand(OctoLogger log, AdoApi adoApi) : base("disable-ado-repo")
        {
            _log = log;
            _adoApi = adoApi;

            Description = "Disables the repo in Azure DevOps. This makes the repo non-readable for all.";

            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoRepo = new Option<string>("--ado-repo")
            {
                IsRequired = true
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Disabling repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");

            var repoId = await _adoApi.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            await _adoApi.DisableRepo(adoOrg, adoTeamProject, repoId);

            _log.LogSuccess("Repo successfully disabled");
        }
    }
}