using System;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter.Handlers;

public class MigrateOrgCommandHandler
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

    public MigrateOrgCommandHandler(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _targetGithubApiFactory = targetGithubApiFactory;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public async Task Invoke(MigrateOrgCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        LogAndValidateOptions(args);

        var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: args.GithubTargetPat);

        var githubEnterpriseId = await githubApi.GetEnterpriseId(args.GithubTargetEnterprise);
        var sourceOrgUrl = GetGithubOrgUrl(args.GithubSourceOrg, null);
        var sourceToken = GetSourceToken(args);

        var migrationId = await githubApi.StartOrganizationMigration(
            sourceOrgUrl,
            args.GithubTargetOrg,
            githubEnterpriseId,
            sourceToken);


        if (!args.Wait)
        {
            _log.LogInformation($"A organization migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        //var migrationState = await githubApi.GetOrganizationMigrationState(migrationId);
        var (migrationState, _, _, failureReason) = await githubApi.GetOrganizationMigration(migrationId);

        while (OrganizationMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(10000);
            (migrationState, _, _, failureReason) = await githubApi.GetOrganizationMigration(migrationId);
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
