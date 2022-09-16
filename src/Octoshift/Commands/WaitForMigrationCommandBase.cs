using System;
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
    private const string REPO_MIGRATION_ID_PREFIX = "RM_";
    private const string ORG_MIGRATION_ID_PREFIX = "OM_";

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

        if (migrationId is null)
        {
            throw new ArgumentNullException(nameof(migrationId));
        }

        if (!migrationId.StartsWith(REPO_MIGRATION_ID_PREFIX) && !migrationId.StartsWith(ORG_MIGRATION_ID_PREFIX))
        {
            throw new OctoshiftCliException($"Invalid migration id: {migrationId}");
        }

        _log.RegisterSecret(githubPat);

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);

        if (migrationId.StartsWith(REPO_MIGRATION_ID_PREFIX))
        {
            await WaitForRepositoryMigration(migrationId, githubPat, githubApi);
        }
        else
        {
            await WaitForOrgMigration(migrationId, githubPat, githubApi);
        }
    }

    private async Task WaitForOrgMigration(string migrationId, string githubPat, GithubApi githubApi)
    {
        var state = await githubApi.GetOrganizationMigrationState(migrationId);

        _log.LogInformation($"Waiting for org migration (ID: {migrationId}) to finish...");

        if (githubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        while (true)
        {
            if (OrganizationMigrationStatus.IsSucceeded(state))
            {
                _log.LogSuccess($"Migration {migrationId} succeeded");
                return;
            }

            if (OrganizationMigrationStatus.IsFailed(state))
            {
                throw new OctoshiftCliException($"Migration {migrationId} failed");
            }

            _log.LogInformation($"Migration {migrationId} is {state}");
            _log.LogInformation($"Waiting {WaitIntervalInSeconds} seconds...");
            await Task.Delay(WaitIntervalInSeconds * 1000);

            state = await githubApi.GetOrganizationMigrationState(migrationId);
        }
    }

    private async Task WaitForRepositoryMigration(string migrationId, string githubPat, GithubApi githubApi)
    {
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
