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
            EnvironmentVariableProvider environmentVariableProvider
        ) : base("migrate-repo")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Import a Bitbucket Server archive to GitHub.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

            var archiveUrl = new Option<string>("--archive-url")
            {
                IsRequired = true,
                Description = "URL used to download Bitbucket Server migration archive."
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var githubApiUrl = new Option<string>("--github-api-url")
            {
                Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
            };
            var wait = new Option("--wait")
            {
                Description = "Synchronously waits for the repo migration to finish."
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(archiveUrl);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(githubPat);
            AddOption(githubApiUrl);
            AddOption(wait);
            AddOption(verbose);

            Handler = CommandHandler.Create<MigrateRepoCommandArgs>(Invoke);
        }

        public async Task Invoke(MigrateRepoCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");

            if (args.GithubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }
            if (args.GithubApiUrl is not null)
            {
                _log.LogInformation($"GITHUB API URL: {args.GithubApiUrl}");
            }
            if (args.Wait)
            {
                _log.LogInformation("WAIT: true");
            }

            args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubApi = _githubApiFactory.Create(apiUrl: args.GithubApiUrl, targetPersonalAccessToken: args.GithubPat);
            var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
            var migrationSourceId = await githubApi.CreateBbsMigrationSource(githubOrgId);

            string migrationId;

            try
            {
                migrationId = await githubApi.StartMigration(
                    migrationSourceId,
                    "https://not-used",  // source repository URL
                    githubOrgId,
                    args.GithubRepo,
                    "not-used",  // source access token
                    args.GithubPat,
                    args.ArchiveUrl,
                    "https://not-used"  // metadata archive URL
                );
            }
            catch (OctoshiftCliException ex)
            {
                if (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
                {
                    _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
                    return;
                }

                throw;
            }

            if (!args.Wait)
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

    public class MigrateRepoCommandArgs
    {
        public string ArchiveUrl { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string GithubPat { get; set; }
        public string GithubApiUrl { get; set; }
        public bool Wait { get; set; }
        public bool Verbose { get; set; }
    }
}
