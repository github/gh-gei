using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.BbsToGithub.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public MigrateRepoCommand(
            OctoLogger log,
            GithubApiFactory githubApiFactory,
            EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Import a Bitbucket Server archive to GitHub.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var archiveUrl = new Option<string>("--archive-url")
            {
                IsRequired = true,
                Description = "URL used to downlodad Bitbucket Server migration archive."
            };
            var wait = new Option("--wait")
            {
                Description = "Synchronously waits for the repo migration to finish."
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var githubApiUrl = new Option<string>("--github-api-url")
            {
                Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(archiveUrl);
            AddOption(wait);
            AddOption(githubPat);
            AddOption(githubApiUrl);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string githubRepo, string archiveUrl, bool wait = false, string githubPat = null, string githubApiUrl = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");

            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }
            if (githubApiUrl is not null)
            {
                _log.LogInformation($"GITHUB API URL: {githubApiUrl}");
            }
            if (wait)
            {
                _log.LogInformation("WAIT: true");
            }

            githubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubApi = _githubApiFactory.Create(apiUrl: githubApiUrl, targetPersonalAccessToken: githubPat);
            var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
            var migrationSourceId = await githubApi.CreateBbsMigrationSource(githubOrgId);

            string migrationId;

            try
            {
                migrationId = await githubApi.StartMigration(
                    migrationSourceId,
                    "https://not-used",  // source repository URL
                    githubOrgId,
                    githubRepo,
                    "not-used",  // source access token
                    githubPat,
                    archiveUrl,
                    "https://not-used"  // metadata archive URL
                );
            }
            catch (OctoshiftCliException ex)
            {
                if (ex.Message == $"A repository called {githubOrg}/{githubRepo} already exists")
                {
                    _log.LogWarning($"The Org '{githubOrg}' already contains a repository with the name '{githubRepo}'. No operation will be performed");
                    return;
                }

                throw;
            }

            if (!wait)
            {
                _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
                return;
            }

            var (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);

            while (RepositoryMigrationStatus.IsPending(migrationState))
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);
            }

            if (RepositoryMigrationStatus.IsFailed(migrationState))
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                throw new OctoshiftCliException(failureReason);
            }

            _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        }
    }
}
