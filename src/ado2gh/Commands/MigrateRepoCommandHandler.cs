using System;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class MigrateRepoCommandHandler
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public MigrateRepoCommandHandler(OctoLogger log, GithubApiFactory githubApiFactory, EnvironmentVariableProvider environmentVariableProvider)
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;
        }

        public async Task Invoke(MigrateRepoCommandArgs args)
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

            if (args.TargetRepoVisibility.HasValue())
            {
                _log.LogInformation($"TARGET REPO VISIBILITY: {args.TargetRepoVisibility}");
            }
            if (args.Wait)
            {
                _log.LogInformation("WAIT: true");
            }
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }
            if (args.GithubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

            var adoRepoUrl = GetAdoRepoUrl(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);

            args.AdoPat ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
            var migrationSourceId = await githubApi.CreateAdoMigrationSource(githubOrgId, null);

            string migrationId;

            try
            {
                migrationId = await githubApi.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, args.GithubRepo, args.AdoPat, args.GithubPat, targetRepoVisibility: args.TargetRepoVisibility);
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

            if (!args.Wait)
            {
                _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
                return;
            }

            var (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);

            while (RepositoryMigrationStatus.IsPending(migrationState))
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);
            }

            if (RepositoryMigrationStatus.IsFailed(migrationState))
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                throw new OctoshiftCliException(failureReason);
            }

            _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        }

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}
