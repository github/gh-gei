using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Handlers;

public class WaitForMigrationCommandHandler : ICommandHandler<WaitForMigrationCommandArgs>
{
    internal int WaitIntervalInSeconds = 10;

    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private const string REPO_MIGRATION_ID_PREFIX = "RM_";
    private const string ORG_MIGRATION_ID_PREFIX = "OM_";

    public WaitForMigrationCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(WaitForMigrationCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;
        _log.RegisterSecret(args.GithubPat);

        if (args.MigrationId is null)
        {
            throw new ArgumentNullException(nameof(args), "MigrationId cannot be null");
        }

        if (!args.MigrationId.StartsWith(REPO_MIGRATION_ID_PREFIX) && !args.MigrationId.StartsWith(ORG_MIGRATION_ID_PREFIX))
        {
            throw new OctoshiftCliException($"Invalid migration id: {args.MigrationId}");
        }

        if (args.MigrationId.StartsWith(REPO_MIGRATION_ID_PREFIX))
        {
            await WaitForRepositoryMigration(args.MigrationId, args.GithubPat, _githubApi);
        }
        else
        {
            await WaitForOrgMigration(args.MigrationId, args.GithubPat, _githubApi);
        }
    }

    private async Task WaitForOrgMigration(string migrationId, string githubPat, GithubApi githubApi)
    {
        var (state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount) = await githubApi.GetOrganizationMigration(migrationId);

        _log.LogInformation($"Waiting for {sourceOrgUrl} -> {targetOrgName} migration (ID: {migrationId}) to finish...");

        if (githubPat is not null)
        {
            _log.LogInformation($"GITHUB PAT: ***");
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
                throw new OctoshiftCliException($"Migration {migrationId} failed for {sourceOrgUrl} -> {targetOrgName}. Failure reason: {failureReason}");
            }

            if (OrganizationMigrationStatus.IsRepoMigration(state))
            {
                var completedRepositoriesCount = (int)totalRepositoriesCount - (int)remainingRepositoriesCount;
                _log.LogInformation($"Migration {migrationId} is {state} - {completedRepositoriesCount}/{totalRepositoriesCount} repositories completed");
            }
            else
            {
                _log.LogInformation($"Migration {migrationId} is {state}");
            }
            _log.LogInformation($"Waiting {WaitIntervalInSeconds} seconds...");
            await Task.Delay(WaitIntervalInSeconds * 1000);

            (state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount) = await githubApi.GetOrganizationMigration(migrationId);
        }
    }

    private async Task WaitForRepositoryMigration(string migrationId, string githubPat, GithubApi githubApi)
    {
        var (state, repositoryName, failureReason) = await githubApi.GetMigration(migrationId);

        _log.LogInformation($"Waiting for {repositoryName} migration (ID: {migrationId}) to finish...");

        if (githubPat is not null)
        {
            _log.LogInformation($"GITHUB PAT: ***");
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
