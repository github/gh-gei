using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private bool _isRetry;

        public MigrateRepoCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub API's to migrate the repo and all PR data.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead.";

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

            var githubApi = _targetGithubApiFactory.Create();
            var sourceGithubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var targetGithubPat = _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubOrgId = await githubApi.GetOrganizationId(githubTargetOrg);
            var migrationSourceId = await githubApi.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, ssh);
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
                var failureReason = await githubApi.GetMigrationFailureReason(migrationId);

                if (!_isRetry && failureReason.Contains("Warning: Permanently added") && failureReason.Contains("(ECDSA) to the list of known hosts"))
                {
                    _log.LogWarning(failureReason);
                    _log.LogWarning("This is a known issue. Retrying the migration should resolve it. Retrying migration now...");

                    _isRetry = true;
                    await githubApi.DeleteRepo(githubTargetOrg, targetRepo);
                    await Invoke(githubSourceOrg, sourceRepo, githubTargetOrg, targetRepo, ssh, verbose);
                }
                else
                {
                    _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                    throw new OctoshiftCliException(failureReason);
                }
            }
            else
            {
                _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
            }
        }

        private string GetGithubRepoUrl(string org, string repo) => $"https://github.com/{org}/{repo}".Replace(" ", "%20");
    }
}