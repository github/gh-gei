using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly Lazy<GithubApi> _lazyGithubApi;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public MigrateRepoCommand(OctoLogger log, Lazy<GithubApi> lazyGithubApi, EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _lazyGithubApi = lazyGithubApi;
            _environmentVariableProvider = environmentVariableProvider;

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
            var ssh = new Option("--ssh")
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
            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string sourceRepo, string githubTargetOrg, string targetRepo, bool ssh = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"SOURCE REPO: {sourceRepo}");
            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"TARGET REPO: {targetRepo}");
            if (ssh)
            {
                _log.LogInformation("SSH: true");
            }

            if (string.IsNullOrWhiteSpace(targetRepo))
            {
                _log.LogInformation($"Target Repo name not provided, defaulting to same as source repo ({sourceRepo})");
                targetRepo = sourceRepo;
            }

            var githubRepoUrl = GetGithubRepoUrl(githubSourceOrg, sourceRepo);

            var githubApi = _lazyGithubApi.Value;
            var githubPat = _environmentVariableProvider.GithubPersonalAccessToken();
            var githubOrgId = await githubApi.GetOrganizationId(githubTargetOrg);
            var migrationSourceId = await githubApi.CreateGhecMigrationSource(githubOrgId, githubPat, ssh);
            var migrationId = await githubApi.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, targetRepo);

            var migrationState = await githubApi.GetMigrationState(migrationId);

            while (migrationState.Trim().ToUpper() is "IN_PROGRESS" or "QUEUED")
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                migrationState = await githubApi.GetMigrationState(migrationId);
            }

            if (migrationState.Trim().ToUpper() == "FAILED")
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                var failureReason = await githubApi.GetMigrationFailureReason(migrationId);
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