using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Handlers;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public MigrateRepoCommandHandler(OctoLogger log, GithubApi githubApi, EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _githubApi = githubApi;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Migrating Repo...");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        _log.LogInformation($"ADO REPO: {args.AdoRepo}");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
        if (args.Wait)
        {
            _log.LogInformation("WAIT: true");
        }
        if (args.QueueOnly)
        {
            _log.LogInformation("QUEUE ONLY: true");
        }
        if (args.TargetRepoVisibility.HasValue())
        {
            _log.LogInformation($"TARGET REPO VISIBILITY: {args.TargetRepoVisibility}");
        }
        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }
        if (args.GithubPat is not null)
        {
            _log.LogInformation("GITHUB PAT: ***");
        }

        _log.RegisterSecret(args.AdoPat);
        _log.RegisterSecret(args.GithubPat);

        if (args.Wait)
        {
            _log.LogWarning("--wait flag is obsolete and will be removed in a future version. The default behavior is now to wait.");
        }

        if (args.Wait && args.QueueOnly)
        {
            throw new OctoshiftCliException("You can't specify both --wait and --queue-only at the same time.");
        }

        if (!args.Wait && !args.QueueOnly)
        {
            _log.LogWarning("The default behavior has changed from only queueing the migration, to waiting for the migration to finish. If you ran this as part of a script to run multiple migrations in parallel, consider using the new --queue-only option to preserve the previous default behavior. This warning will be removed in a future version.");
        }

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();

        var adoRepoUrl = GetAdoRepoUrl(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);

        args.AdoPat ??= _environmentVariableProvider.AdoPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);
        var migrationSourceId = await _githubApi.CreateAdoMigrationSource(githubOrgId, null);

        string migrationId;

        try
        {
            migrationId = await _githubApi.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, args.GithubRepo, args.AdoPat, args.GithubPat, targetRepoVisibility: args.TargetRepoVisibility);
        }
        catch (OctoshiftCliException ex)
        {
            if (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
            {
                _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
                return;
            }

            throw;
        }

        if (args.QueueOnly)
        {
            _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(10000);
            (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);
        }

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            _log.LogError($"Migration Failed. Migration ID: {migrationId}");
            _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-target-org {args.GithubOrg} --target-repo {args.GithubRepo}`");
            throw new OctoshiftCliException(failureReason);
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-target-org {args.GithubOrg} --target-repo {args.GithubRepo}`");
    }

    private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org.EscapeDataString()}/{project.EscapeDataString()}/_git/{repo.EscapeDataString()}";
}
