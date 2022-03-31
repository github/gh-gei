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
            Description += "Note: Expects GH_PAT env variables to be set.";

            var migrationId = new Option<string>("--migration-id")
            {
                IsRequired = true,
                Description = "Waits for the specified migration to finish."
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose") { IsRequired = false };

            AddOption(migrationId);
            AddOption(githubTargetPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, bool>(Invoke);
        }

        public async Task Invoke(string migrationId, string githubTargetPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation($"Waiting for migration {migrationId} to finish...");

            if (githubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }

            var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: githubTargetPat);

            while (true)
            {
                var specifiedMigrationState = await githubApi.GetMigrationState(migrationId);
                switch (specifiedMigrationState)
                {
                    case RepositoryMigrationStatus.Failed:
                        var failureReason = await githubApi.GetMigrationFailureReason(migrationId);
                        _log.LogError($"Migration failed for migration {migrationId}");
                        throw new OctoshiftCliException(failureReason);
                    case RepositoryMigrationStatus.Succeeded:
                        _log.LogSuccess($"Migration succeeded for migration {migrationId}");
                        return;
                    default: // IN_PROGRESS, QUEUED
                        _log.LogInformation($"Migration {migrationId} is {specifiedMigrationState}");
                        break;
                }

                _log.LogInformation($"Waiting {WaitIntervalInSeconds} seconds...");
                await Task.Delay(WaitIntervalInSeconds * 1000);
            }
        }
    }
}
