using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class WaitForMigrationCommand : Command
    {
        internal int WaitIntervalInSeconds = 10;

        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;

        public WaitForMigrationCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base("wait-for-migration")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;

            Description = "Waits for migration(s) to finish and reports all in progress and queued ones.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-target-pat option to be set.";

            var migrationId = new Option<string>("--migration-id")
            {
                IsRequired = true,
                Description = "Waits for the specified migration to finish."
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var targetApiUrl = new Option<string>("--target-api-url")
            {
                IsRequired = false,
                Description = "The URL of the target API, if not migrating to github.com (default: https://api.github.com)."
            };
            var verbose = new Option("--verbose") { IsRequired = false };

            AddOption(migrationId);
            AddOption(githubTargetPat);
            AddOption(targetApiUrl);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string migrationId, string githubTargetPat = null, string targetApiUrl = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            var githubApi = _targetGithubApiFactory.Create(targetApiUrl, githubTargetPat);
            var (state, repositoryName, failureReason) = await githubApi.GetMigration(migrationId);

            _log.LogInformation($"Waiting for {repositoryName} migration (ID: {migrationId}) to finish...");

            if (githubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }

            if (targetApiUrl is not null)
            {
                _log.LogInformation($"TARGET API URL: {targetApiUrl}");
            }

            while (true)
            {
                if (RepositoryMigrationStatus.IsSucceeded(state))
                {
                    _log.LogSuccess($"Migration {migrationId} succeeded for {repositoryName}");
                    return;
                }
                else if (RepositoryMigrationStatus.IsFailed(state))
                {
                    _log.LogError($"Migration {migrationId} failed for {repositoryName}");
                    throw new OctoshiftCliException(failureReason);
                }

                _log.LogInformation($"Migration {migrationId} for {repositoryName} is {state}");
                _log.LogInformation($"Waiting {WaitIntervalInSeconds} seconds...");
                await Task.Delay(WaitIntervalInSeconds * 1000);

                (state, repositoryName, failureReason) = await githubApi.GetMigration(migrationId);
            }
        }
    }
}
