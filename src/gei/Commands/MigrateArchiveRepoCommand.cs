using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;


namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateArchiveRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly IAzureApiFactory _azureApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        private const int Timeout_In_Hours = 10;
        private const int Delay_In_Milliseconds = 10000; // 10 seconds

        public MigrateArchiveRepoCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider, IAzureApiFactory azureApiFactory) : base("migrate-archive-repo")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _azureApiFactory = azureApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to the source GHES API.";

            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "The api endpoint for the hostname of your GHES instance. For example: https://api.myghes.com"
            };
            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = false
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(ghesApiUrl);
            AddOption(githubSourceOrg);
            AddOption(sourceRepo);
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string ghesApiUrl, string githubSourceOrg, string sourceRepo, string azureStorageConnectionString, bool noSslVerify = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            // Log all the parameters, except for the connection string, which is a secret
            _log.LogInformation("Starting Migration Archives...");
            _log.LogInformation($"GHES SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"GHES SOURCE REPO: {sourceRepo}");

            if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
            {
                _log.LogInformation("--azure-storage-connection-string not set, using environment variable AZURE_STORAGE_CONNECTION_STRING");
            }

            if (string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                _log.LogInformation("--ghes-api-url not provided, defaulting to https://api.github.com");
                ghesApiUrl = "https://api.github.com";
            }
            else
            {
                _log.LogInformation($"GHES API URL: {ghesApiUrl}");
            }

            if (noSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            var githubApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSSL(ghesApiUrl) : _sourceGithubApiFactory.Create(ghesApiUrl);
            var azureApi = noSslVerify ? _azureApiFactory.CreateClientNoSSL(azureStorageConnectionString) : _azureApiFactory.Create(azureStorageConnectionString);

            var gitDataArchiveId = await githubApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await githubApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var metadataArchiveUrl = await WaitForArchiveGeneration(githubApi, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive(metadata) download url: {metadataArchiveUrl}");
            var gitArchiveUrl = await WaitForArchiveGeneration(githubApi, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive(git) download url: {gitArchiveUrl}");

            var timeNow = $"{dateTimeNow:yyyy-MM-dd_HH-mm-ss}";

            // TODO: Update these with the real file names
            var gitArchiveFileName = $"{timeNow}gitArchive.tar.gz";
            var metadataArchiveFileName = $"{timeNow}metadataArchive.tar.gz";

            var gitArchiveFilePath = $"/tmp/{gitArchiveFileName}";
            var metadataArchiveFilePath = $"/tmp/{metadataArchiveFileName}";

            // Download both archives to the local filesystem
            _log.LogInformation($"Downloading archive from {gitArchiveUrl} to {gitArchiveFilePath}");
            await azureApi.DownloadFileTo(gitArchiveUrl, gitArchiveFilePath);
            _log.LogInformation($"Downloading archive from {metadataArchiveUrl} to {metadataArchiveFilePath}");
            await azureApi.DownloadFileTo(metadataArchiveUrl, metadataArchiveFilePath);

            _log.LogInformation($"Uploading archive {gitArchiveFileName} to Azure Blob Storage");
            var authenticatedGitUrl = await azureApi.UploadToBlob(gitArchiveFileName, gitArchiveFilePath);
            _log.LogInformation($"Uploading archive {metadataArchiveFileName} to Azure Blob Storage");
            var authenticatedMetadataUrl = await azureApi.UploadToBlob(metadataArchiveFileName, metadataArchiveFilePath);

            _log.LogInformation($"Deleting local archive files");
            // delete the files
            // File.Delete(gitArchiveFilePath);
            // File.Delete(metadataArchiveFilePath);

            // run migrate repo command
            var migrateRepoCommand = new MigrateRepoCommand(_log, _targetGithubApiFactory, _environmentVariableProvider);
            await migrateRepoCommand.Invoke(
                githubSourceOrg,
                "",
                "",
                sourceRepo,
                githubTargetOrg,
                targetRepo,
                "",
                authenticatedMetadataUrl.ToString(),
                authenticatedGitUrl.ToString(),
                false,
                verbose
            );
        }

        private async Task<string> WaitForArchiveGeneration(GithubApi githubApi, string githubSourceOrg, int archiveId)
        {
            var timeOut = DateTime.Now.AddHours(Timeout_In_Hours);
            while (DateTime.Now < timeOut)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(githubSourceOrg, archiveId);
                _log.LogInformation($"Waiting for archive with id {archiveId} generation to finish. Current status: {archiveStatus}");
                if (archiveStatus == ArchiveMigrationStatus.Exported)
                {
                    return await githubApi.GetArchiveMigrationUrl(githubSourceOrg, archiveId);
                }
                if (archiveStatus == ArchiveMigrationStatus.Failed)
                {
                    throw new OctoshiftCliException($"Archive generation failed with id: {archiveId}");
                }
                await Task.Delay(Delay_In_Milliseconds);
            }
            throw new TimeoutException($"Archive generation timed out after {Timeout_In_Hours} hours");
        }
    }
}