using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class WaitForMigrationCommand : Command
    {
        internal int WaitIntervalInSeconds = 10;

        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public WaitForMigrationCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("wait-for-migration")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

            Description = "Waits for migration(s) to finish and reports all in progress and queued ones.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

            var migrationId = new Option<string>("--migration-id")
            {
                IsRequired = true,
                Description = "Waits for the specified migration to finish."
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose") { IsRequired = false };

            AddOption(migrationId);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, bool>(Invoke);
        }

        public async Task Invoke(string migrationId, string githubPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            var githubApi = _githubApiFactory.Create(personalAccessToken: githubPat);
            var (state, repositoryName, failureReason) = await githubApi.GetMigration(migrationId);

            _log.LogInformation($"Waiting for {repositoryName} migration (ID: {migrationId}) to finish...");

            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
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
