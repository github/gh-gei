using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands;

public class WaitForMigrationCommandBase : Command
{
    internal int WaitIntervalInSeconds = 10;

    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public WaitForMigrationCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base("wait-for-migration")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;

        Description = "Waits for migration(s) to finish and reports all in progress and queued ones.";
    }

    protected virtual Option<string> MigrationId { get; } = new("--migration-id")
    {
        IsRequired = true,
        Description = "Waits for the specified migration to finish."
    };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat") { IsRequired = false };

    protected virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

    protected void AddOptions()
    {
        AddOption(MigrationId);
        AddOption(GithubPat);
        AddOption(Verbose);
    }

    public async Task Handle(string migrationId, string githubPat = null, bool verbose = false)
    {
        _log.Verbose = verbose;

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);
        var (state, repositoryName, failureReason) = await githubApi.GetMigration(migrationId);

        _log.LogInformation($"Waiting for {repositoryName} migration (ID: {migrationId}) to finish...");

        if (githubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        while (true)
        {
            if (RepositoryMigrationStatus.IsSucceeded(state))
            {
                _log.LogSuccess($"Migration {migrationId} succeeded for {repositoryName}");
                return;
            }

            if (RepositoryMigrationStatus.IsFailed(state))
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
