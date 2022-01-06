using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class LockRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApi _adoApi;

        public LockRepoCommand(OctoLogger log, AdoApi adoApi) : base("lock-ado-repo")
        {
            _log = log;
            _adoApi = adoApi;

            Description = "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo.";

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

            _log.LogInformation("Locking repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");

            var teamProjectId = await _adoApi.GetTeamProjectId(adoOrg, adoTeamProject);
            var repoId = await _adoApi.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            var identityDescriptor = await _adoApi.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            await _adoApi.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor);

            _log.LogSuccess("Repo successfully locked");
        }
    }
}