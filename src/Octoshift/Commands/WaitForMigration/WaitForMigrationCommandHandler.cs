using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands.WaitForMigration;

public class WaitForMigrationCommandHandler : ICommandHandler<WaitForMigrationCommandArgs>
{
    internal int WaitIntervalInSeconds = 10;

    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;


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

        if (args.MigrationId.StartsWith(WaitForMigrationCommandArgs.REPO_MIGRATION_ID_PREFIX))
        {
            await WaitForRepositoryMigration(args.MigrationId, _githubApi);
        }
        else
        {
            await WaitForOrgMigration(args.MigrationId, _githubApi);
        }
    }

    private async Task WaitForOrgMigration(string migrationId, GithubApi githubApi)
    {
        var (state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount) = await githubApi.GetOrganizationMigration(migrationId);

        _log.LogInformation($"Waiting for {sourceOrgUrl} -> {targetOrgName} migration (ID: {migrationId}) to finish...");

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

    private async Task WaitForRepositoryMigration(string migrationId, GithubApi githubApi)
    {
        var (state, repositoryName, warningsCount, failureReason, migrationLogUrl) = await githubApi.GetMigration(migrationId);

        _log.LogInformation($"Waiting for {repositoryName} migration (ID: {migrationId}) to finish...");

        while (true)
        {
            if (RepositoryMigrationStatus.IsSucceeded(state))
            {
                _log.LogSuccess($"Migration {migrationId} succeeded for {repositoryName}");
                LogWarningsCount(warningsCount);
                _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs`");
                return;
            }

            if (RepositoryMigrationStatus.IsFailed(state))
            {
                _log.LogError($"Migration {migrationId} failed for {repositoryName}");
                LogWarningsCount(warningsCount);
                _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs`");
                throw new OctoshiftCliException(failureReason);
            }

            _log.LogInformation($"Migration {migrationId} for {repositoryName} is {state}");
            _log.LogInformation($"Waiting {WaitIntervalInSeconds} seconds...");
            await Task.Delay(WaitIntervalInSeconds * 1000);

            (state, repositoryName, warningsCount, failureReason, migrationLogUrl) = await githubApi.GetMigration(migrationId);
        }
    }

    private void LogWarningsCount(int warningsCount)
    {
        switch (warningsCount)
        {
            case 0:
                _log.LogInformation("No warnings encountered during this migration");
                break;
            case 1:
                _log.LogWarning("1 warning encountered during this migration");
                break;
            default:
                _log.LogWarning($"{warningsCount} warnings encountered during this migration");
                break;
        }
    }
}
