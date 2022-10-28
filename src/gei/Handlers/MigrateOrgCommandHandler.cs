using System;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Handlers;

public class MigrateOrgCommandHandler : ICommandHandler<MigrateOrgCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

    public MigrateOrgCommandHandler(OctoLogger log, GithubApi targetGithubApi, EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _githubApi = targetGithubApi;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public async Task Handle(MigrateOrgCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;
        _log.RegisterSecret(args.GithubSourcePat);
        _log.RegisterSecret(args.GithubTargetPat);

        LogAndValidateOptions(args);

        var githubEnterpriseId = await _githubApi.GetEnterpriseId(args.GithubTargetEnterprise);
        var sourceOrgUrl = GetGithubOrgUrl(args.GithubSourceOrg, null);
        var sourceToken = GetSourceToken(args);

        var migrationId = await _githubApi.StartOrganizationMigration(
            sourceOrgUrl,
            args.GithubTargetOrg,
            githubEnterpriseId,
            sourceToken);


        if (!args.Wait)
        {
            _log.LogInformation($"A organization migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, _, failureReason, remainingRepositoriesCount, totalRepositoriesCount) = await _githubApi.GetOrganizationMigration(migrationId);

        while (OrganizationMigrationStatus.IsPending(migrationState))
        {
            if (OrganizationMigrationStatus.IsRepoMigration(migrationState))
            {
                var migratedRepositoriesCount = (int)totalRepositoriesCount - (int)remainingRepositoriesCount;
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. {migratedRepositoriesCount}/{totalRepositoriesCount} repo(s) migrated. Waiting 10 seconds...");
            }
            else
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            }
            await Task.Delay(10000);
            (migrationState, _, _, failureReason, remainingRepositoriesCount, totalRepositoriesCount) = await _githubApi.GetOrganizationMigration(migrationId);
        }

        if (OrganizationMigrationStatus.IsFailed(migrationState))
        {
            _log.LogError($"Migration Failed. Migration ID: {migrationId}");
            throw new OctoshiftCliException(failureReason);
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
    }

    private string GetSourceToken(MigrateOrgCommandArgs args) =>
        args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

    private string GetGithubOrgUrl(string org, string baseUrl) => $"{baseUrl ?? DEFAULT_GITHUB_BASE_URL}/{org}".Replace(" ", "%20");

    private void LogAndValidateOptions(MigrateOrgCommandArgs args)
    {
        _log.LogInformation("Migrating Org...");
        _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
        _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");
        _log.LogInformation($"GITHUB TARGET ENTERPRISE: {args.GithubTargetEnterprise}");

        if (args.GithubSourcePat.HasValue())
        {
            _log.LogInformation("GITHUB SOURCE PAT: ***");
        }

        if (args.GithubTargetPat.HasValue())
        {
            _log.LogInformation("GITHUB TARGET PAT: ***");

            if (args.GithubSourcePat.IsNullOrWhiteSpace())
            {
                args.GithubSourcePat = args.GithubTargetPat;
                _log.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
            }
        }
    }
}
