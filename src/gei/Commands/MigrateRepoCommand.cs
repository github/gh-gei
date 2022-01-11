using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.gei.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubFactory;

        public MigrateRepoCommand(OctoLogger log, GithubApiFactory githubFactory) : base("migrate-repo")
        {
            _log = log;
            _githubFactory = githubFactory;

            Description = "Invokes the GitHub API's to migrate the repo and all PR data";

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true
            };
            var targetRepo = new Option<string>("--target-repo")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(sourceRepo);
            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string sourceRepo, string githubTargetOrg, string targetRepo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"SOURCE REPO: {sourceRepo}");
            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"TARGET REPO: {targetRepo}");

            if (string.IsNullOrWhiteSpace(targetRepo))
            {
                _log.LogInformation($"Target Repo name not provided, defaulting to same as source repo ({sourceRepo})");
                targetRepo = sourceRepo;
            }

            var githubRepoUrl = GetGithubRepoUrl(githubSourceOrg, sourceRepo);

            using var github = _githubFactory.Create();
            var githubPat = _githubFactory.GetGithubToken();
            var githubOrgId = await github.GetOrganizationId(githubTargetOrg);
            var migrationSourceId = await github.CreateGHECMigrationSource(githubOrgId, githubPat);
            var migrationId = await github.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, targetRepo);

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

        private string GetGithubRepoUrl(string org, string repo) => $"https://github.com/{org}/{repo}".Replace(" ", "%20");
    }
}