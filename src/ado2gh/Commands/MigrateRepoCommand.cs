using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public MigrateRepoCommand(
            OctoLogger log,
            GithubApiFactory githubApiFactory,
            EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub API's to migrate the repo and all PR data";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.";

            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoRepo = new Option<string>("--ado-repo")
            {
                IsRequired = true
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var ssh = new Option("--ssh")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Uses SSH protocol instead of HTTPS to push a Git repository into the target repository on GitHub."
            };
            var wait = new Option("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the repo migration to finish."
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(ssh);
            AddOption(wait);
            AddOption(adoPat);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool, bool, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool ssh = false, bool wait = false, string adoPat = null, string githubPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"ADO REPO: {adoRepo}");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            if (ssh)
            {
                _log.LogWarning("SSH mode is no longer supported. --ssh flag will be ignored.");
            }
            if (wait)
            {
                _log.LogInformation("WAIT: true");
            }
            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            githubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubApi = _githubApiFactory.Create(personalAccessToken: githubPat);
            if (await githubApi.RepoExists(githubOrg, githubRepo))
            {
                _log.LogWarning($"The Org '{githubOrg}' already contains a repository with the name '{githubRepo}'. No operation will be performed");
                return;
            }

            var adoRepoUrl = GetAdoRepoUrl(adoOrg, adoTeamProject, adoRepo);

            adoPat ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
            var migrationSourceId = await githubApi.CreateAdoMigrationSource(githubOrgId, null);
            var migrationId = await githubApi.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo, adoPat, githubPat);

            if (!wait)
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
