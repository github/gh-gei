using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Handlers;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _sourceGithubApi;
    private readonly GithubApi _targetGithubApi;
    private readonly AzureApi _azureApi;
    private readonly AwsApi _awsApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly HttpDownloadService _httpDownloadService;
    private const int ARCHIVE_GENERATION_TIMEOUT_IN_HOURS = 10;
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000; // 10 seconds
    private const string GIT_ARCHIVE_FILE_NAME = "git_archive.tar.gz";
    private const string METADATA_ARCHIVE_FILE_NAME = "metadata_archive.tar.gz";
    private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

    public MigrateRepoCommandHandler(OctoLogger log, GithubApi sourceGithubApi, GithubApi targetGithubApi, EnvironmentVariableProvider environmentVariableProvider, AzureApi azureApi, AwsApi awsApi, HttpDownloadService httpDownloadService)
    {
        _log = log;
        _sourceGithubApi = sourceGithubApi;
        _targetGithubApi = targetGithubApi;
        _environmentVariableProvider = environmentVariableProvider;
        _azureApi = azureApi;
        _awsApi = awsApi;
        _httpDownloadService = httpDownloadService;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;
        _log.RegisterSecret(args.AdoPat);
        _log.RegisterSecret(args.GithubSourcePat);
        _log.RegisterSecret(args.GithubTargetPat);
        _log.RegisterSecret(args.AzureStorageConnectionString);

        LogOptions(args);
        ValidateOptions(args);

        if (args.GhesApiUrl.HasValue())
        {
            (args.GitArchiveUrl, args.MetadataArchiveUrl) = await GenerateAndUploadArchive(
              args.GithubSourceOrg,
              args.SourceRepo,
              args.AwsBucketName,
              args.SkipReleases,
              args.LockSourceRepo
            );

            _log.LogInformation("Archives uploaded to Azure Blob Storage, now starting migration...");
        }

        var githubOrgId = await _targetGithubApi.GetOrganizationId(args.GithubTargetOrg);
        var sourceRepoUrl = GetSourceRepoUrl(args);
        var sourceToken = GetSourceToken(args);
        var targetToken = args.GithubTargetPat ?? _environmentVariableProvider.TargetGithubPersonalAccessToken();
        var migrationSourceId = args.GithubSourceOrg.HasValue()
            ? await _targetGithubApi.CreateGhecMigrationSource(githubOrgId)
            : await _targetGithubApi.CreateAdoMigrationSource(githubOrgId, args.AdoServerUrl);

        string migrationId;

        try
        {
            migrationId = await _targetGithubApi.StartMigration(
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

        var (migrationState, _, failureReason) = await _targetGithubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(10000);
            (migrationState, _, failureReason) = await _targetGithubApi.GetMigration(migrationId);
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
      string githubSourceOrg,
      string sourceRepo,
      string awsBucketName,
      bool skipReleases,
      bool lockSourceRepo)
    {
        var gitDataArchiveId = await _sourceGithubApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
        _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
        var metadataArchiveId = await _sourceGithubApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo, skipReleases, lockSourceRepo);
        _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

        var timeNow = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        var gitArchiveFileName = $"{timeNow}-{gitDataArchiveId}-{GIT_ARCHIVE_FILE_NAME}";
        var metadataArchiveFileName = $"{timeNow}-{metadataArchiveId}-{METADATA_ARCHIVE_FILE_NAME}";

        var gitArchiveUrl = await WaitForArchiveGeneration(_sourceGithubApi, githubSourceOrg, gitDataArchiveId);
        _log.LogInformation($"Archive (git) download url: {gitArchiveUrl}");

        _log.LogInformation($"Downloading archive from {gitArchiveUrl}");
        var gitArchiveContent = await _httpDownloadService.DownloadToBytes(gitArchiveUrl);

        var metadataArchiveUrl = await WaitForArchiveGeneration(_sourceGithubApi, githubSourceOrg, metadataArchiveId);
        _log.LogInformation($"Archive (metadata) download url: {metadataArchiveUrl}");

        _log.LogInformation($"Downloading archive from {metadataArchiveUrl}");
        var metadataArchiveContent = await _httpDownloadService.DownloadToBytes(metadataArchiveUrl);

        return _awsApi.HasValue() ?
            await UploadArchivesToAws(awsBucketName, gitArchiveFileName, gitArchiveContent, metadataArchiveFileName, metadataArchiveContent) :
            await UploadArchivesToAzure(gitArchiveFileName, gitArchiveContent, metadataArchiveFileName, metadataArchiveContent);
    }

    private async Task<(string, string)> UploadArchivesToAzure(string gitArchiveFileName, byte[] gitArchiveContent, string metadataArchiveFileName, byte[] metadataArchiveContent)
    {
        _log.LogInformation($"Uploading archive {gitArchiveFileName} to Azure Blob Storage");
        var authenticatedGitArchiveUri = await _azureApi.UploadToBlob(gitArchiveFileName, gitArchiveContent);
        _log.LogInformation($"Uploading archive {metadataArchiveFileName} to Azure Blob Storage");
        var authenticatedMetadataArchiveUri = await _azureApi.UploadToBlob(metadataArchiveFileName, metadataArchiveContent);

        return (authenticatedGitArchiveUri.ToString(), authenticatedMetadataArchiveUri.ToString());
    }

    private async Task<(string, string)> UploadArchivesToAws(string bucketName, string gitArchiveFileName, byte[] gitArchiveContent, string metadataArchiveFileName, byte[] metadataArchiveContent)
    {
        _log.LogInformation($"Uploading archive {gitArchiveFileName} to AWS S3");
        var authenticatedGitArchiveUri = await _awsApi.UploadToBucket(bucketName, gitArchiveContent, gitArchiveFileName);
        _log.LogInformation($"Uploading archive {metadataArchiveFileName} to AWS S3");
        var authenticatedMetadataArchiveUri = await _awsApi.UploadToBucket(bucketName, metadataArchiveContent, metadataArchiveFileName);

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

        if (args.AwsBucketName.HasValue())
        {
            _log.LogInformation($"AWS BUCKET NAME: {args.AwsBucketName}");
        }

        if (args.AwsAccessKey.HasValue())
        {
            _log.LogInformation($"AWS ACCESS KEY: {args.AwsAccessKey}");
        }

        if (args.AwsSecretKey.HasValue())
        {
            _log.LogInformation($"AWS SECRET KEY: {args.AwsSecretKey}");
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

        // GHES migration path
        if (args.GhesApiUrl.HasValue())
        {
            var shouldUseAzureStorage = GetAzureStorageConnectionString(args).HasValue();
            var shouldUseAwsS3 = args.AwsBucketName.HasValue();

            if (!shouldUseAzureStorage && !shouldUseAwsS3)
            {
                throw new OctoshiftCliException(
                    "Either Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) or " +
                    "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY env. variable), --aws-secret-key (or AWS_SECRET_KEY env.variable)) " +
                    "must be provided.");
            }

            if (shouldUseAzureStorage && shouldUseAwsS3)
            {
                throw new OctoshiftCliException(
                    "Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) and " +
                    "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY env. variable), --aws-secret-key (or AWS_SECRET_Key env.variable)) cannot be " +
                    "specified together.");
            }

            if (shouldUseAwsS3)
            {
                if (!GetAwsAccessKey(args).HasValue())
                {
                    throw new OctoshiftCliException("Either --aws-access-key or AWS_ACCESS_KEY environment variable must be set.");
                }

                if (!GetAwsSecretKey(args).HasValue())
                {
                    throw new OctoshiftCliException("Either --aws-secret-key or AWS_SECRET_KEY environment variable must be set.");
                }
            }
            else if (args.AwsAccessKey.HasValue() || args.AwsSecretKey.HasValue())
            {
                throw new OctoshiftCliException("--aws-access-key and --aws-secret-key can only be provided with --aws-bucket-name.");
            }
        }
        else
        {
            if (args.AwsBucketName.HasValue())
            {
                throw new OctoshiftCliException("--ghes-api-url must be specified when --aws-bucket-name is specified.");
            }

            if (args.NoSslVerify)
            {
                throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
            }
        }
    }

    private string GetAwsAccessKey(MigrateRepoCommandArgs args) => args.AwsAccessKey.HasValue() ? args.AwsAccessKey : _environmentVariableProvider.AwsAccessKey(false);

    private string GetAwsSecretKey(MigrateRepoCommandArgs args) => args.AwsSecretKey.HasValue() ? args.AwsSecretKey : _environmentVariableProvider.AwsSecretKey(false);

    private string GetAzureStorageConnectionString(MigrateRepoCommandArgs args) => args.AzureStorageConnectionString.HasValue()
        ? args.AzureStorageConnectionString
        : _environmentVariableProvider.AzureStorageConnectionString();
}
