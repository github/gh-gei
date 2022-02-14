using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly IAzureApiFactory _azureApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private bool _isRetry;
        private const int ARCHIVE_GENERATION_TIMEOUT_IN_HOURS = 10;
        private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000; // 10 seconds
        private const string GIT_ARCHIVE_FILE_NAME = "git_archive.tar.gz";
        private const string METADATA_ARCHIVE_FILE_NAME = "metadata_archive.tar.gz";

        public MigrateRepoCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider, IAzureApiFactory azureApiFactory) : base("migrate-repo")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _azureApiFactory = azureApiFactory;

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

            // GHES migration path
            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "If migrating from GHES, the api endpoint for the hostname of your GHES instance. For example: http(s)://api.myghes.com"
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = false,
                Description = "(Required when used with --ghes-api-url) The connection string for the Azure storage account, used to upload data archives pre-migration. For example: DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "(Only effective when passed in with --ghes-api-url) Disables SSL verification. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted. All other migration steps will continue to verify SSL."
            };

            // Pre-uploaded archive urls, hidden by default
            var gitArchiveUrl = new Option<string>("--git-archive-url")
            {
                IsHidden = true,
                IsRequired = false,
                Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated git archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --metadata-archive-url"
            };
            var metadataArchiveUrl = new Option<string>("--metadata-archive-url")
            {
                IsHidden = true,
                IsRequired = false,
                Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated metadata archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --git-archive-url"
            };

            var ssh = new Option("--ssh")
            {
                IsRequired = false,
                Description = "Uses SSH protocol instead of HTTPS to push a Git repository into the target repository on GitHub."
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

            AddOption(ghesApiUrl);
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);

            AddOption(gitArchiveUrl);
            AddOption(metadataArchiveUrl);

            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<
              string, string, string, string, string, string, string,
              string, string, bool,
              string, string,
              bool, bool>(Invoke);
        }

        public async Task Invoke(
          string githubSourceOrg,
          string adoSourceOrg,
          string adoTeamProject,
          string sourceRepo,
          string githubTargetOrg,
          string targetRepo,
          string targetApiUrl,
          string ghesApiUrl = "",
          string azureStorageConnectionString = "",
          bool noSslVerify = false,
          string gitArchiveUrl = "",
          string metadataArchiveUrl = "",
          bool ssh = false,
          bool verbose = false)
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

            if (!string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                (gitArchiveUrl, metadataArchiveUrl) = await GenerateAndUploadArchive(
                  ghesApiUrl,
                  githubSourceOrg,
                  sourceRepo,
                  azureStorageConnectionString,
                  noSslVerify
                );

                _log.LogInformation("Archives uploaded to Azure Blob Storage, now starting migration...");
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
                    await Invoke(githubSourceOrg, adoSourceOrg, adoTeamProject, sourceRepo, githubTargetOrg, targetRepo, targetApiUrl, ghesApiUrl, azureStorageConnectionString, noSslVerify, gitArchiveUrl, metadataArchiveUrl, ssh, verbose);
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

        private async Task<(string GitArchiveUrl, string MetadataArchiveUrl)> GenerateAndUploadArchive(
          string ghesApiUrl,
          string githubSourceOrg,
          string sourceRepo,
          string azureStorageConnectionString,
          bool noSslVerify = false)
        {
            _log.LogInformation($"GHES API URL: {ghesApiUrl}");

            if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
            {
                _log.LogInformation("--azure-storage-connection-string not set, using environment variable AZURE_STORAGE_CONNECTION_STRING");
                azureStorageConnectionString = _environmentVariableProvider.AzureStorageConnectionString();

                if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
                {
                    throw new OctoshiftCliException("Please set either --azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING");
                }
            }

            if (noSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            var ghesApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSsl(ghesApiUrl) : _sourceGithubApiFactory.Create(ghesApiUrl);
            var azureApi = noSslVerify ? _azureApiFactory.CreateClientNoSsl(azureStorageConnectionString) : _azureApiFactory.Create(azureStorageConnectionString);

            var gitDataArchiveId = await ghesApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await ghesApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var gitArchiveUrl = await WaitForArchiveGeneration(ghesApi, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive (git) download url: {gitArchiveUrl}");
            var metadataArchiveUrl = await WaitForArchiveGeneration(ghesApi, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive (metadata) download url: {metadataArchiveUrl}");

            var timeNow = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            var gitArchiveFileName = $"{timeNow}-{gitDataArchiveId}-{GIT_ARCHIVE_FILE_NAME}";
            var metadataArchiveFileName = $"{timeNow}-{metadataArchiveId}-{METADATA_ARCHIVE_FILE_NAME}";

            _log.LogInformation($"Downloading archive from {gitArchiveUrl}");
            var gitArchiveContent = await azureApi.DownloadArchive(gitArchiveUrl);
            _log.LogInformation($"Downloading archive from {metadataArchiveUrl}");
            var metadataArchiveContent = await azureApi.DownloadArchive(metadataArchiveUrl);

            _log.LogInformation($"Uploading archive {gitArchiveFileName} to Azure Blob Storage");
            var authenticatedGitArchiveUri = await azureApi.UploadToBlob(gitArchiveFileName, gitArchiveContent);
            _log.LogInformation($"Uploading archive {metadataArchiveFileName} to Azure Blob Storage");
            var authenticatedMetadataArchiveUri = await azureApi.UploadToBlob(metadataArchiveFileName, metadataArchiveContent);

            return (authenticatedGitArchiveUri.ToString(), authenticatedMetadataArchiveUri.ToString());
        }

        private async Task<string> WaitForArchiveGeneration(GithubApi githubApi, string githubSourceOrg, int archiveId)
        {
            var timeout = DateTime.Now.AddHours(ARCHIVE_GENERATION_TIMEOUT_IN_HOURS);
            while (DateTime.Now < timeout)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(githubSourceOrg, archiveId);
                _log.LogInformation($"Waiting for archive with id {archiveId} generation to finish. Current status: {archiveStatus}");
                if (archiveStatus == ArchiveMigrationStatus.Exported)
                {
                    return await githubApi.GetArchiveMigrationUrl(githubSourceOrg, archiveId);
                }
                if (archiveStatus == ArchiveMigrationStatus.Failed)
                {
                    throw new OctoshiftCliException($"Archive generation failed for id: {archiveId}");
                }
                await Task.Delay(CHECK_STATUS_DELAY_IN_MILLISECONDS);
            }
            throw new TimeoutException($"Archive generation timed out after {ARCHIVE_GENERATION_TIMEOUT_IN_HOURS} hours");
        }

        private string GetGithubRepoUrl(string org, string repo) => $"https://github.com/{org}/{repo}".Replace(" ", "%20");

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}
