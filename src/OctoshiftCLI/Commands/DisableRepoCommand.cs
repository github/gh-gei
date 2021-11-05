using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class DisableRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoFactory;

        public DisableRepoCommand(OctoLogger log, AdoApiFactory adoFactory) : base("disable-ado-repo")
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
            _log.LogInformation("Disabling repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");

            using var ado = _adoFactory.Create();

            var repoId = await ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            await ado.DisableRepo(adoOrg, adoTeamProject, repoId);

            _log.LogSuccess("Repo successfully disabled");
        }
    }
}