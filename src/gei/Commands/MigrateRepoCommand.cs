﻿using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly IAzureApiFactory _azureApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private const int ARCHIVE_GENERATION_TIMEOUT_IN_HOURS = 10;
        private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000; // 10 seconds
        private const string GIT_ARCHIVE_FILE_NAME = "git_archive.tar.gz";
        private const string METADATA_ARCHIVE_FILE_NAME = "metadata_archive.tar.gz";
        private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

        public MigrateRepoCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider, IAzureApiFactory azureApiFactory) : base(
            name: "migrate-repo",
            description: "Invokes the GitHub APIs to migrate the repo and all repo data.")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _azureApiFactory = azureApiFactory;

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
            };
            var adoServerUrl = new Option<string>("--ado-server-url")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
            };
            var adoSourceOrg = new Option<string>("--ado-source-org")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Uses ADO_PAT env variable or --ado-pat option."
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false,
                IsHidden = true
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-target-pat option."
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
                Description = "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The connection string for the Azure storage account, used to upload data archives pre-migration. For example: \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\""
            };
            var noSslVerify = new Option<bool>("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
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
            var skipReleases = new Option<bool>("--skip-releases")
            {
                IsRequired = false,
                Description = "Skip releases when migrating."
            };
            var lockSourceRepo = new Option<bool>("--lock-source-repo")
            {
                IsRequired = false,
                Description = "Lock source repo when migrating."
            };
            var wait = new Option<bool>("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the repo migration to finish."
            };
            var githubSourcePat = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false,
                IsHidden = true
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(adoServerUrl);
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

            AddOption(skipReleases);
            AddOption(lockSourceRepo);

            AddOption(wait);
            AddOption(githubSourcePat);
            AddOption(githubTargetPat);
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<MigrateRepoCommandArgs>(Invoke);
        }

        public async Task Invoke(MigrateRepoCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            LogOptions(args);
            ValidateOptions(args);

            _log.RegisterSecret(args.GithubSourcePat);
            _log.RegisterSecret(args.GithubTargetPat);
            _log.RegisterSecret(args.AdoPat);

            if (args.GhesApiUrl.HasValue())
            {
                (args.GitArchiveUrl, args.MetadataArchiveUrl) = await GenerateAndUploadArchive(
                  args.GhesApiUrl,
                  args.GithubSourceOrg,
                  args.SourceRepo,
                  args.AzureStorageConnectionString,
                  args.GithubSourcePat,
                  args.SkipReleases,
                  args.LockSourceRepo,
                  args.NoSslVerify
                );

                _log.LogInformation("Archives uploaded to Azure Blob Storage, now starting migration...");
            }

            var githubApi = _targetGithubApiFactory.Create(args.TargetApiUrl, args.GithubTargetPat);

            var githubOrgId = await githubApi.GetOrganizationId(args.GithubTargetOrg);
            var sourceRepoUrl = GetSourceRepoUrl(args);
            var sourceToken = GetSourceToken(args);
            var targetToken = args.GithubTargetPat ?? _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var migrationSourceId = args.GithubSourceOrg.HasValue()
                ? await githubApi.CreateGhecMigrationSource(githubOrgId)
                : await githubApi.CreateAdoMigrationSource(githubOrgId, args.AdoServerUrl);

            string migrationId;

            try
            {
                migrationId = await githubApi.StartMigration(
                    migrationSourceId,
                    sourceRepoUrl,
                    githubOrgId,
                    args.TargetRepo,
                    sourceToken,
                    targetToken,
                    args.GitArchiveUrl,
                    args.MetadataArchiveUrl,
                    args.SkipReleases,
                    args.GhesApiUrl.IsNullOrWhiteSpace() && args.LockSourceRepo);
            }
            catch (OctoshiftCliException ex)
            {
                if (ex.Message == $"A repository called {args.GithubTargetOrg}/{args.TargetRepo} already exists")
                {
                    _log.LogWarning($"The Org '{args.GithubTargetOrg}' already contains a repository with the name '{args.TargetRepo}'. No operation will be performed");
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

        private string GetSourceToken(MigrateRepoCommandArgs args) =>
            args.GithubSourceOrg.HasValue()
                ? args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken()
                : args.AdoPat ?? _environmentVariableProvider.AdoPersonalAccessToken();

        private string GetSourceRepoUrl(MigrateRepoCommandArgs args) =>
            args.GithubSourceOrg.HasValue()
                ? GetGithubRepoUrl(args.GithubSourceOrg, args.SourceRepo, args.GhesApiUrl.HasValue() ? ExtractGhesBaseUrl(args.GhesApiUrl) : null)
                : GetAdoRepoUrl(args.AdoServerUrl, args.AdoSourceOrg, args.AdoTeamProject, args.SourceRepo);

        private string ExtractGhesBaseUrl(string ghesApiUrl)
        {
            // We expect the GHES url template to be either http(s)://hostname/api/v3 or http(s)://api.hostname.com.
            // We are either going to be able to extract and return the base url based on the above templates or 
            // will fallback to ghesApiUrl and return it as the base url. 

            ghesApiUrl = ghesApiUrl.Trim().TrimEnd('/');

            var baseUrl = Regex.Match(ghesApiUrl, @"(?<baseUrl>https?:\/\/.+)\/api\/v3", RegexOptions.IgnoreCase).Groups["baseUrl"].Value;
            if (baseUrl.HasValue())
            {
                return baseUrl;
            }

            var match = Regex.Match(ghesApiUrl, @"(?<scheme>https?):\/\/api\.(?<host>.+)", RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups["scheme"]}://{match.Groups["host"]}" : ghesApiUrl;
        }

        private async Task<(string GitArchiveUrl, string MetadataArchiveUrl)> GenerateAndUploadArchive(
          string ghesApiUrl,
          string githubSourceOrg,
          string sourceRepo,
          string azureStorageConnectionString,
          string githubSourcePat,
          bool skipReleases,
          bool lockSourceRepo,
          bool noSslVerify = false)
        {
            if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
            {
                _log.LogInformation("--azure-storage-connection-string not set, using environment variable AZURE_STORAGE_CONNECTION_STRING");
                azureStorageConnectionString = _environmentVariableProvider.AzureStorageConnectionString();

                if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
                {
                    throw new OctoshiftCliException("Please set either --azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING");
                }
            }

            var ghesApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSsl(ghesApiUrl, githubSourcePat) : _sourceGithubApiFactory.Create(ghesApiUrl, githubSourcePat);
            var azureApi = noSslVerify ? _azureApiFactory.CreateClientNoSsl(azureStorageConnectionString) : _azureApiFactory.Create(azureStorageConnectionString);

            var gitDataArchiveId = await ghesApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await ghesApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo, skipReleases, lockSourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var timeNow = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            var gitArchiveFileName = $"{timeNow}-{gitDataArchiveId}-{GIT_ARCHIVE_FILE_NAME}";
            var metadataArchiveFileName = $"{timeNow}-{metadataArchiveId}-{METADATA_ARCHIVE_FILE_NAME}";

            var gitArchiveUrl = await WaitForArchiveGeneration(ghesApi, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive (git) download url: {gitArchiveUrl}");

            _log.LogInformation($"Downloading archive from {gitArchiveUrl}");
            var gitArchiveContent = await azureApi.DownloadArchive(gitArchiveUrl);

            var metadataArchiveUrl = await WaitForArchiveGeneration(ghesApi, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive (metadata) download url: {metadataArchiveUrl}");

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

        private string GetGithubRepoUrl(string org, string repo, string baseUrl) => $"{baseUrl ?? DEFAULT_GITHUB_BASE_URL}/{org}/{repo}".Replace(" ", "%20");

        private string GetAdoRepoUrl(string serverUrl, string org, string project, string repo)
        {
            serverUrl = serverUrl.HasValue() ? serverUrl.TrimEnd('/') : "https://dev.azure.com";
            return $"{serverUrl}/{org}/{project}/_git/{repo}".Replace(" ", "%20");
        }

        private void LogOptions(MigrateRepoCommandArgs args)
        {
            _log.LogInformation("Migrating Repo...");

            var hasAdoSpecificArg = new[] { args.AdoPat, args.AdoServerUrl, args.AdoSourceOrg, args.AdoTeamProject }.Any(arg => arg.HasValue());
            if (hasAdoSpecificArg)
            {
                _log.LogWarning("ADO migration feature will be removed from `gh gei` in near future, please consider switching to `gh ado2gh` for ADO migrations instead.");
            }

            if (args.GithubSourceOrg.HasValue())
            {
                _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
            }
            if (args.AdoServerUrl.HasValue())
            {
                _log.LogInformation($"ADO SERVER URL: {args.AdoServerUrl}");
            }
            if (args.AdoSourceOrg.HasValue())
            {
                _log.LogInformation($"ADO SOURCE ORG: {args.AdoSourceOrg}");
            }
            if (args.AdoTeamProject.HasValue())
            {
                _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            }
            _log.LogInformation($"SOURCE REPO: {args.SourceRepo}");
            _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");
            _log.LogInformation($"TARGET REPO: {args.TargetRepo}");

            if (args.TargetApiUrl.HasValue())
            {
                _log.LogInformation($"TARGET API URL: {args.TargetApiUrl}");
            }

            if (args.Wait)
            {
                _log.LogInformation("WAIT: true");
            }

            if (args.GithubSourcePat.HasValue())
            {
                _log.LogInformation("GITHUB SOURCE PAT: ***");
            }

            if (args.GithubTargetPat.HasValue())
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }

            if (args.AdoPat.HasValue())
            {
                _log.LogInformation("ADO PAT: ***");
            }

            if (args.GhesApiUrl.HasValue())
            {
                _log.LogInformation($"GHES API URL: {args.GhesApiUrl}");
            }

            if (args.AzureStorageConnectionString.HasValue())
            {
                _log.LogInformation("AZURE STORAGE CONNECTION STRING: ***");
            }

            if (args.NoSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            if (args.SkipReleases)
            {
                _log.LogInformation("SKIP RELEASES: true");
            }

            if (args.GitArchiveUrl.HasValue())
            {
                _log.LogInformation($"GIT ARCHIVE URL: {args.GitArchiveUrl}");
            }

            if (args.MetadataArchiveUrl.HasValue())
            {
                _log.LogInformation($"METADATA ARCHIVE URL: {args.MetadataArchiveUrl}");
            }

            if (args.LockSourceRepo)
            {
                _log.LogInformation("LOCK SOURCE REPO: true");
            }
        }

        private void ValidateOptions(MigrateRepoCommandArgs args)
        {
            if (args.GithubTargetPat.HasValue() && args.GithubSourcePat.IsNullOrWhiteSpace())
            {
                args.GithubSourcePat = args.GithubTargetPat;
                _log.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
            }

            if (args.GithubSourceOrg.IsNullOrWhiteSpace() && args.AdoSourceOrg.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
            }

            if (args.AdoServerUrl.HasValue() && args.AdoSourceOrg.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Must specify --ado-source-org with the collection name when using --ado-server-url");
            }

            if (args.GithubSourceOrg.IsNullOrWhiteSpace() && args.AdoSourceOrg.HasValue() && args.AdoTeamProject.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("When using --ado-source-org you must also provide --ado-team-project");
            }

            if (args.TargetRepo.IsNullOrWhiteSpace())
            {
                _log.LogInformation($"Target repo name not provided, defaulting to same as source repo ({args.SourceRepo})");
                args.TargetRepo = args.SourceRepo;
            }

            if (string.IsNullOrWhiteSpace(args.GitArchiveUrl) != string.IsNullOrWhiteSpace(args.MetadataArchiveUrl))
            {
                throw new OctoshiftCliException("When using archive urls, you must provide both --git-archive-url --metadata-archive-url");
            }
        }
    }

    public class MigrateRepoCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string AdoServerUrl { get; set; }
        public string AdoSourceOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string SourceRepo { get; set; }
        public string GithubTargetOrg { get; set; }
        public string TargetRepo { get; set; }
        public string TargetApiUrl { get; set; }
        public string GhesApiUrl { get; set; }
        public string AzureStorageConnectionString { get; set; }
        public bool NoSslVerify { get; set; }
        public string GitArchiveUrl { get; set; }
        public string MetadataArchiveUrl { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool Wait { get; set; }
        public bool Verbose { get; set; }
        public string GithubSourcePat { get; set; }
        public string GithubTargetPat { get; set; }
        public string AdoPat { get; set; }
    }
}
