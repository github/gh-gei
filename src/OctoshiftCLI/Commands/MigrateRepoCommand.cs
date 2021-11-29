using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoFactory;
        private readonly GithubApiFactory _githubFactory;

        public MigrateRepoCommand(OctoLogger log, AdoApiFactory adoFactory, GithubApiFactory githubFactory) : base("migrate-repo")
        {
            _log = log;
            _adoFactory = adoFactory;
            _githubFactory = githubFactory;

            Description = "Invokes the GitHub API's to migrate the repo and all PR data";

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
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
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
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");

            var adoRepoUrl = GetAdoRepoUrl(adoOrg, adoTeamProject, adoRepo);

            using var github = _githubFactory.Create();
            var adoToken = _adoFactory.GetAdoToken();

            var githubOrgId = await github.GetOrganizationId(githubOrg);
            var migrationSourceId = await github.CreateMigrationSource(githubOrgId, adoToken);
            var migrationId = await github.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo);

            var migrationState = await github.GetMigrationState(migrationId);

            while (migrationState.Trim().ToUpper() is "IN_PROGRESS" or "QUEUED")
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                migrationState = await github.GetMigrationState(migrationId);
            }

            if (migrationState.Trim().ToUpper() == "FAILED")
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                var failureReason = await github.GetMigrationFailureReason(migrationId);
                _log.LogError(failureReason);
            }
            else
            {
                _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
            }
        }

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}