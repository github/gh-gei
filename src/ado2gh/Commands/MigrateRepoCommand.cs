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
        private bool _isRetry;

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
            Description += "Note: Expects ADO_PAT and GH_PAT env variables to be set.";

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
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool ssh = false, bool verbose = false)
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
                _log.LogInformation("SSH: true");
            }

            var adoRepoUrl = GetAdoRepoUrl(adoOrg, adoTeamProject, adoRepo);

            var adoToken = _environmentVariableProvider.AdoPersonalAccessToken();
            var githubPat = _environmentVariableProvider.GithubPersonalAccessToken();
            var githubApi = _githubApiFactory.Create();
            var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
            var migrationSourceId = await githubApi.CreateAdoMigrationSource(githubOrgId, adoToken, githubPat, ssh);
            var migrationId = await githubApi.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo);

            var migrationState = await githubApi.GetMigrationState(migrationId);

            while (migrationState.Trim().ToUpper() is "IN_PROGRESS" or "QUEUED")
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                migrationState = await githubApi.GetMigrationState(migrationId);
            }

            if (migrationState.Trim().ToUpper() == "FAILED")
            {
                var failureReason = await githubApi.GetMigrationFailureReason(migrationId);

                if (!_isRetry && failureReason.Contains("Warning: Permanently added") && failureReason.Contains("(ECDSA) to the list of known hosts"))
                {
                    _log.LogWarning(failureReason);
                    _log.LogWarning("This is a known issue. Retrying the migration should resolve it. Retrying migration now...");

                    _isRetry = true;
                    await githubApi.DeleteRepo(githubOrg, githubRepo);
                    await Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, ssh, verbose);
                }
                else
                {
                    _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                    throw new OctoshiftCliException(failureReason);
                }
            }
            else
            {
                _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
            }
        }

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}
