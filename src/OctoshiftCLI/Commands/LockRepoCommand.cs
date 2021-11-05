using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class LockRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoFactory;

        public LockRepoCommand(OctoLogger log, AdoApiFactory adoFactory) : base("lock-ado-repo")
        {
            _log = log;
            _adoFactory = adoFactory;

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

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);

            Handler = CommandHandler.Create<string, string, string>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo)
        {
            _log.LogInformation("Locking repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");

            using var ado = _adoFactory.Create();

            var teamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var repoId = await ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            var identityDescriptor = await ado.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            await ado.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor);

            _log.LogSuccess("Repo successfully locked");
        }
    }
}