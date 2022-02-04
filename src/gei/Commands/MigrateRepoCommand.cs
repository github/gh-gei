using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public MigrateRepoCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub API's to migrate the repo and all PR data.";

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable. Will fall back to GH_PAT if not set."
            };
            var adoSourceOrg = new Option<string>("--ado-source-org")
            {
                IsRequired = false,
                Description = "Uses ADO_PAT env variable."
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };
            var targetRepo = new Option<string>("--target-repo")
            {
                IsRequired = false,
                Description = "Defaults to the name of source-repo"
            };
            var ssh = new Option("--ssh")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(adoSourceOrg);
            AddOption(adoTeamProject);
            AddOption(sourceRepo);
            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string adoSourceOrg, string adoTeamProject, string sourceRepo, string githubTargetOrg, string targetRepo, bool ssh = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Migrating Repo...");
            if (!string.IsNullOrWhiteSpace(githubSourceOrg))
            {
                _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");
            }
            if (!string.IsNullOrWhiteSpace(adoSourceOrg))
            {
                _log.LogInformation($"ADO SOURCE ORG: {adoSourceOrg}");
                _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            }
            _log.LogInformation($"SOURCE REPO: {sourceRepo}");
            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"TARGET REPO: {targetRepo}");
            if (ssh)
            {
                _log.LogInformation("SSH: true");
            }

            if (string.IsNullOrWhiteSpace(githubSourceOrg) && string.IsNullOrWhiteSpace(adoSourceOrg))
            {
                throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
            }

            if (string.IsNullOrWhiteSpace(githubSourceOrg) && !string.IsNullOrWhiteSpace(adoSourceOrg) && string.IsNullOrWhiteSpace(adoTeamProject))
            {
                throw new OctoshiftCliException("When using --ado-source-org you must also provide --ado-team-project");
            }

            if (string.IsNullOrWhiteSpace(targetRepo))
            {
                _log.LogInformation($"Target Repo name not provided, defaulting to same as source repo ({sourceRepo})");
                targetRepo = sourceRepo;
            }

            var githubApi = _targetGithubApiFactory.Create();
            var targetGithubPat = _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubOrgId = await githubApi.GetOrganizationId(githubTargetOrg);
            string sourceRepoUrl;
            string migrationSourceId;

            if (string.IsNullOrWhiteSpace(githubSourceOrg))
            {
                sourceRepoUrl = GetAdoRepoUrl(adoSourceOrg, adoTeamProject, sourceRepo);
                var sourceAdoPat = _environmentVariableProvider.AdoPersonalAccessToken();
                migrationSourceId = await githubApi.CreateAdoMigrationSource(githubOrgId, sourceAdoPat, targetGithubPat, ssh);
            }
            else
            {
                sourceRepoUrl = GetGithubRepoUrl(githubSourceOrg, sourceRepo);
                var sourceGithubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
                migrationSourceId = await githubApi.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, ssh);
            }

            var migrationId = await githubApi.StartMigration(migrationSourceId, sourceRepoUrl, githubOrgId, targetRepo);
            var migrationState = await githubApi.GetMigrationState(migrationId);

            while (migrationState.Trim().ToUpper() is "IN_PROGRESS" or "QUEUED")
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                migrationState = await githubApi.GetMigrationState(migrationId);
            }

            if (migrationState.Trim().ToUpper() == "FAILED")
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                var failureReason = await githubApi.GetMigrationFailureReason(migrationId);
                _log.LogError(failureReason);
            }
            else
            {
                _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
            }
        }

        private string GetGithubRepoUrl(string org, string repo) => $"https://github.com/{org}/{repo}".Replace(" ", "%20");

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}
