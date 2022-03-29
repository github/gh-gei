using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class DisableRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public DisableRepoCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base("disable-ado-repo")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

            Description = "Disables the repo in Azure DevOps. This makes the repo non-readable for all.";
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

            var ado = _adoApiFactory.Create();

            var allRepos = await ado.GetRepos(adoOrg, adoTeamProject);
            if (allRepos.Any(r => r.Name == adoRepo && r.IsDisabled))
            {
                _log.LogSuccess($"Repo '{adoOrg}/{adoTeamProject}/{adoRepo}' is already disabled - No action will be performed");
                return;
            }
            var repoId = allRepos.First(r => r.Name == adoRepo).Id;
            await ado.DisableRepo(adoOrg, adoTeamProject, repoId);

            _log.LogSuccess("Repo successfully disabled");
        }
    }
}
