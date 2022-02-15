using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub.Commands;

public class WaitForMigrationCommand: Command
{
    private const int WAIT_INTERVAL_IN_SECONDS = 10;
    
    private readonly OctoLogger _log;
    private readonly GithubApiFactory _githubApiFactory;

    public WaitForMigrationCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("wait-for-migration")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;

        Description = "Waits for migration(s) to finish and reports all in progress and queued ones.";
        Description += Environment.NewLine;
        Description += "Note: Expects GH_PAT env variables to be set.";

        var githubOrg = new Option<string>("--github-org")
        {
            IsRequired = true
        };
        var migrationId = new Option<string>("--migration-id")
        {
            IsRequired = false,
            Description = "Waits for the specified migration to finish."
        };
        var verbose = new Option("--verbose")
        {
            IsRequired = false
        };
        
        AddOption(githubOrg);
        AddOption(migrationId);
        AddOption(verbose);

        Handler = CommandHandler.Create<string, string, bool>(Invoke);
    }

    public async Task Invoke(string githubOrg, string migrationId = null, bool verbose = false)
    {
        _log.Verbose = verbose;

        var hasMigrationId = !migrationId.IsNullOrWhiteSpace();

        _log.LogInformation($"Waiting for {(hasMigrationId ? $"migration {migrationId}" : "all migrations")} to finish...");
        _log.LogInformation($"GITHUB ORG: {githubOrg}");
        if (hasMigrationId)
        {
            _log.LogInformation($"MIGRATION ID: {migrationId}");
        }

        var githubApi = _githubApiFactory.Create();
        var githubOrgId = await githubApi.GetOrganizationId(githubOrg);

        var ongoingMigrations = new Dictionary<string, string>();
        do
        {
            if (hasMigrationId)
            {
                var specifiedMigrationState = await githubApi.GetMigrationState(migrationId);
                switch (specifiedMigrationState)
                {
                    case RepositoryMigrationStatus.Failed:
                    {
                        var failureReason = await githubApi.GetMigrationFailureReason(migrationId);
                        _log.LogError($"Migration Failed for migration {migrationId}");
                        throw new OctoshiftCliException(failureReason);
                    }
                    case RepositoryMigrationStatus.Succeeded:
                        _log.LogSuccess($"Migration succeeded for migration {migrationId}");
                        return;
                    default: // IN_PROGRESS, QUEUED
                        _log.LogInformation($"Migration {migrationId} is {specifiedMigrationState}");
                        break;
                }
            }

            ongoingMigrations = (await githubApi.GetMigrationStates(githubOrgId))
                .Where(mig => mig.MigrationId == migrationId 
                              || IsOngoingMigration(mig.State) 
                              || ongoingMigrations.ContainsKey(mig.MigrationId))
                .ToDictionary(x => x.MigrationId, x => x.State);
            
            var totalInProgress = ongoingMigrations.Count(kvp => kvp.Value == RepositoryMigrationStatus.InProgress);
            var totalQueued = ongoingMigrations.Count(kvp => kvp.Value == RepositoryMigrationStatus.Queued);

            _log.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: {totalInProgress}, " +
                                $"Total migrations {RepositoryMigrationStatus.Queued}: {totalQueued}");
            _log.LogInformation($"Waiting {WAIT_INTERVAL_IN_SECONDS} seconds...");
                                
            await Task.Delay(WAIT_INTERVAL_IN_SECONDS * 1000);
        } while (ongoingMigrations.Any(kvp => IsOngoingMigration(kvp.Value)));
    }

    private bool IsOngoingMigration(string state) => state is
        RepositoryMigrationStatus.Queued or 
        RepositoryMigrationStatus.InProgress;
}
