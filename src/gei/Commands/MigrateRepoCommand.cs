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
        private bool _isRetry;

        public MigrateRepoCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub APIs to migrate the repo and all repo data.";

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
            var targetApiUrl = new Option<string>("--target-api-url")
            {
                IsRequired = false,
                Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
            };
            var gitArchiveUrl = new Option<string>("--git-archive-url")
            {
                IsRequired = false,
                Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated git archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --metadata-archive-url"
            };
            var metadataArchiveUrl = new Option<string>("--metadata-archive-url")
            {
                IsRequired = false,
                Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated metadata archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --git-archive-url"
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
            AddOption(targetApiUrl);
            AddOption(gitArchiveUrl);
            AddOption(metadataArchiveUrl);
            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string adoSourceOrg, string adoTeamProject, string sourceRepo, string githubTargetOrg, string targetRepo, string targetApiUrl, string gitArchiveUrl = "", string metadataArchiveUrl = "", bool ssh = false, bool verbose = false)
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
            if (string.IsNullOrWhiteSpace(targetApiUrl))
            {
                targetApiUrl = "https://api.github.com";
            }

            _log.LogInformation($"Target API URL: {targetApiUrl}");

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
                _log.LogInformation($"Target repo name not provided, defaulting to same as source repo ({sourceRepo})");
                targetRepo = sourceRepo;
            }

            if (string.IsNullOrWhiteSpace(gitArchiveUrl) != string.IsNullOrWhiteSpace(metadataArchiveUrl))
            {
                throw new OctoshiftCliException("When using archive urls, you must provide both --git-archive-url --metadata-archive-url");
            }
            else if (!string.IsNullOrWhiteSpace(metadataArchiveUrl))
            {
                _log.LogInformation($"GIT ARCHIVE URL: {gitArchiveUrl}");
                _log.LogInformation($"METADATA ARCHIVE URL: {metadataArchiveUrl}");
            }

            var githubApi = _targetGithubApiFactory.Create(targetApiUrl);
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

            var migrationId = await githubApi.StartMigration(migrationSourceId, sourceRepoUrl, githubOrgId, targetRepo, gitArchiveUrl, metadataArchiveUrl);
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
                    await githubApi.DeleteRepo(githubTargetOrg, targetRepo);
                    await Invoke(githubSourceOrg, adoSourceOrg, adoTeamProject, sourceRepo, githubTargetOrg, targetRepo, "", ssh: ssh, verbose: verbose);
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

        private string GetGithubRepoUrl(string org, string repo) => $"https://github.com/{org}/{repo}".Replace(" ", "%20");

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}
