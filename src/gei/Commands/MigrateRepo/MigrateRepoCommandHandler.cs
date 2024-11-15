using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _sourceGithubApi;
    private readonly GithubApi _targetGithubApi;
    private readonly AzureApi _azureApi;
    private readonly AwsApi _awsApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly HttpDownloadService _httpDownloadService;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly GhesVersionChecker _ghesVersionChecker;
    private readonly RetryPolicy _retryPolicy;
    private readonly WarningsCountLogger _warningsCountLogger;
    private const int ARCHIVE_GENERATION_TIMEOUT_IN_HOURS = 20;
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000; // 10 seconds
    private const string GIT_ARCHIVE_FILE_NAME = "git_archive.tar.gz";
    private const string METADATA_ARCHIVE_FILE_NAME = "metadata_archive.tar.gz";
    private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

    public MigrateRepoCommandHandler(
        OctoLogger log,
        GithubApi sourceGithubApi,
        GithubApi targetGithubApi,
        EnvironmentVariableProvider environmentVariableProvider,
        AzureApi azureApi,
        AwsApi awsApi,
        HttpDownloadService httpDownloadService,
        FileSystemProvider fileSystemProvider,
        GhesVersionChecker ghesVersionChecker,
        RetryPolicy retryPolicy,
        WarningsCountLogger warningsCountLogger)
    {
        _log = log;
        _sourceGithubApi = sourceGithubApi;
        _targetGithubApi = targetGithubApi;
        _environmentVariableProvider = environmentVariableProvider;
        _azureApi = azureApi;
        _awsApi = awsApi;
        _httpDownloadService = httpDownloadService;
        _fileSystemProvider = fileSystemProvider;
        _ghesVersionChecker = ghesVersionChecker;
        _retryPolicy = retryPolicy;
        _warningsCountLogger = warningsCountLogger;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Repo...");

        var blobCredentialsRequired = await _ghesVersionChecker.AreBlobCredentialsRequired(args.GhesApiUrl);

        if (args.GhesApiUrl.HasValue())
        {
            ValidateGHESOptions(args, blobCredentialsRequired);
        }

        if (args.GhesApiUrl.HasValue())
        {
            var targetRepoExists = await _targetGithubApi.DoesRepoExist(args.GithubTargetOrg, args.TargetRepo);
            var targetOrgExists = await _targetGithubApi.DoesOrgExist(args.GithubTargetOrg);

            if (targetRepoExists)
            {
                throw new OctoshiftCliException($"A repository called {args.GithubTargetOrg}/{args.TargetRepo} already exists");
            }

            if (!targetOrgExists)
            {
                throw new OctoshiftCliException($"The target org \"{args.GithubTargetOrg}\" does not exist.");
            }
        }

        string migrationSourceId;

        var githubOrgId = await _targetGithubApi.GetOrganizationId(args.GithubTargetOrg);

        try
        {
            migrationSourceId = await _targetGithubApi.CreateGhecMigrationSource(githubOrgId);
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
        {
            var insufficientPermissionsMessage = InsufficientPermissionsMessageGenerator.Generate(args.GithubTargetOrg);
            var message = $"{ex.Message}{insufficientPermissionsMessage}";
            throw new OctoshiftCliException(message, ex);
        }

        if (args.GhesApiUrl.HasValue())
        {
            (args.GitArchiveUrl, args.MetadataArchiveUrl) = await GenerateAndUploadArchive(
              args.GithubSourceOrg,
              args.GithubTargetOrg,
              args.SourceRepo,
              args.AwsBucketName,
              args.SkipReleases,
              args.LockSourceRepo,
              blobCredentialsRequired,
              args.KeepArchive,
              args.UseGithubStorage
            );

            if (args.UseGithubStorage || blobCredentialsRequired)
            {
                _log.LogInformation("Archives uploaded to blob storage, now starting migration...");
            }
        }

        var sourceRepoUrl = GetSourceRepoUrl(args);
        var sourceToken = GetSourceToken(args);
        var targetToken = args.GithubTargetPat ?? _environmentVariableProvider.TargetGithubPersonalAccessToken();

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
                args.TargetRepoVisibility,
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

        if (args.QueueOnly)
        {
            _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _targetGithubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(10000);
            (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _targetGithubApi.GetMigration(migrationId);
        }

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            _log.LogError($"Migration Failed. Migration ID: {migrationId}");
            _warningsCountLogger.LogWarningsCount(warningsCount);
            _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-target-org {args.GithubTargetOrg} --target-repo {args.TargetRepo}`");
            Console.WriteLine($"Migration ID: {migrationId}, Org ID: {githubOrgId}, Source ID: {migrationSourceId}");

            Console.WriteLine($"Error during migration: {failureReason}");
            throw new OctoshiftCliException(failureReason);
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        _warningsCountLogger.LogWarningsCount(warningsCount);
        _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-target-org {args.GithubTargetOrg} --target-repo {args.TargetRepo}`");
    }

    private string GetSourceToken(MigrateRepoCommandArgs args) => args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

    private string GetSourceRepoUrl(MigrateRepoCommandArgs args) => GetGithubRepoUrl(args.GithubSourceOrg, args.SourceRepo, args.GhesApiUrl.HasValue() ? ExtractGhesBaseUrl(args.GhesApiUrl) : null);

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
      string githubTargetOrg,
      string sourceRepo,
      string awsBucketName,
      bool skipReleases,
      bool lockSourceRepo,
      bool blobCredentialsRequired,
      bool keepArchive,
      bool useGithubStorage)
    {
        var (gitArchiveUrl, metadataArchiveUrl, gitArchiveId, metadataArchiveId) = await _retryPolicy.Retry(
            async () => await GenerateArchive(githubSourceOrg, sourceRepo, skipReleases, lockSourceRepo));

        if (!useGithubStorage && !blobCredentialsRequired)
        {
            return (gitArchiveUrl, metadataArchiveUrl);
        }

        var timeNow = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        var gitArchiveUploadFileName = $"{timeNow}-{gitArchiveId}-{GIT_ARCHIVE_FILE_NAME}";
        var metadataArchiveUploadFileName = $"{timeNow}-{metadataArchiveId}-{METADATA_ARCHIVE_FILE_NAME}";
        var gitArchiveDownloadFilePath = _fileSystemProvider.GetTempFileName();
        var metadataArchiveDownloadFilePath = _fileSystemProvider.GetTempFileName();
        try
        {
            _log.LogInformation($"Downloading archive from {gitArchiveUrl}");
            await _httpDownloadService.DownloadToFile(gitArchiveUrl, gitArchiveDownloadFilePath);

            _log.LogInformation($"Downloading archive from {metadataArchiveUrl}");
            await _httpDownloadService.DownloadToFile(metadataArchiveUrl, metadataArchiveDownloadFilePath);

#pragma warning disable IDE0063
            await using (var gitArchiveContent = _fileSystemProvider.OpenRead(gitArchiveDownloadFilePath))
            await using (var metadataArchiveContent = _fileSystemProvider.OpenRead(metadataArchiveDownloadFilePath))
#pragma warning restore IDE0063
            {
                if (useGithubStorage)
                {
                    return await UploadArchivesToGithub(
                        githubTargetOrg,
                        gitArchiveUploadFileName,
                        gitArchiveContent,
                        metadataArchiveUploadFileName,
                        metadataArchiveContent
                    );
                }
#pragma warning disable IDE0046
                else if (_awsApi.HasValue())
#pragma warning restore IDE0046
                {
                    return await UploadArchivesToAws(
                        awsBucketName,
                        gitArchiveUploadFileName,
                        gitArchiveContent,
                        metadataArchiveUploadFileName,
                        metadataArchiveContent
                    );
                }
                else
                {
                    return await UploadArchivesToAzure(
                        gitArchiveUploadFileName,
                        gitArchiveContent,
                        metadataArchiveUploadFileName,
                        metadataArchiveContent
                    );
                }
            }
        }
        finally
        {
            if (!keepArchive)
            {
                DeleteArchive(gitArchiveDownloadFilePath);
                DeleteArchive(metadataArchiveDownloadFilePath);
            }
        }
    }

    private async Task<(string GitArchiveUrl, string MetadataArchiveUrl, int GitArchiveId, int MetadataArchiveId)> GenerateArchive(
        string githubSourceOrg,
        string sourceRepo,
        bool skipReleases,
        bool lockSourceRepo)
    {
        var gitArchiveId = await _sourceGithubApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
        _log.LogInformation($"Archive generation of git data started with id: {gitArchiveId}");
        var metadataArchiveId = await _sourceGithubApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo, skipReleases, lockSourceRepo);
        _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

        var gitArchiveUrl = await WaitForArchiveGeneration(_sourceGithubApi, githubSourceOrg, gitArchiveId);
        _log.LogInformation($"Archive (git) download url: {gitArchiveUrl}");

        var metadataArchiveUrl = await WaitForArchiveGeneration(_sourceGithubApi, githubSourceOrg, metadataArchiveId);
        _log.LogInformation($"Archive (metadata) download url: {metadataArchiveUrl}");

        return (gitArchiveUrl, metadataArchiveUrl, gitArchiveId, metadataArchiveId);
    }

    private void DeleteArchive(string path)
    {
        try
        {
            _fileSystemProvider.DeleteIfExists(path);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _log.LogWarning($"Couldn't delete the downloaded archive at \"{path}\". Error message: \"{ex.Message}\"");
            _log.LogVerbose(ex.ToString());
        }
    }

    private async Task<(string, string)> UploadArchivesToAzure(string gitArchiveFileName, Stream gitArchiveContent, string metadataArchiveFileName, Stream metadataArchiveContent)
    {
        _log.LogInformation($"Uploading archive {gitArchiveFileName} to Azure Blob Storage");
        var authenticatedGitArchiveUri = await _azureApi.UploadToBlob(gitArchiveFileName, gitArchiveContent);
        _log.LogInformation($"Uploading archive {metadataArchiveFileName} to Azure Blob Storage");
        var authenticatedMetadataArchiveUri = await _azureApi.UploadToBlob(metadataArchiveFileName, metadataArchiveContent);

        return (authenticatedGitArchiveUri.ToString(), authenticatedMetadataArchiveUri.ToString());
    }

    private async Task<(string, string)> UploadArchivesToAws(string bucketName, string gitArchiveFileName, Stream gitArchiveContent, string metadataArchiveFileName, Stream metadataArchiveContent)
    {
        _log.LogInformation($"Uploading archive {gitArchiveFileName} to AWS S3");
        var authenticatedGitArchiveUri = await _awsApi.UploadToBucket(bucketName, gitArchiveContent, gitArchiveFileName);
        _log.LogInformation($"Uploading archive {metadataArchiveFileName} to AWS S3");
        var authenticatedMetadataArchiveUri = await _awsApi.UploadToBucket(bucketName, metadataArchiveContent, metadataArchiveFileName);

        return (authenticatedGitArchiveUri.ToString(), authenticatedMetadataArchiveUri.ToString());
    }

    private async Task<(string, string)> UploadArchivesToGithub(string org, string gitArchiveUploadFileName, Stream gitArchiveContent, string metadataArchiveUploadFileName, Stream metadataArchiveContent)
    {
        var githubOrgDatabaseId = await _targetGithubApi.GetOrganizationDatabaseId(org);

        _log.LogInformation($"Uploading git archive to GitHub Storage");
        var uploadedGitArchiveUrl = await _targetGithubApi.UploadArchiveToGithubStorage(githubOrgDatabaseId, gitArchiveUploadFileName, gitArchiveContent);

        _log.LogInformation($"Uploading metadata archive to GitHub Storage");
        var uploadedMetadataArchiveUrl = await _targetGithubApi.UploadArchiveToGithubStorage(githubOrgDatabaseId, metadataArchiveUploadFileName, metadataArchiveContent);

        return (uploadedGitArchiveUrl, uploadedMetadataArchiveUrl);
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

    private string GetGithubRepoUrl(string org, string repo, string baseUrl) => $"{baseUrl ?? DEFAULT_GITHUB_BASE_URL}/{org.EscapeDataString()}/{repo.EscapeDataString()}";

    private void ValidateGHESOptions(MigrateRepoCommandArgs args, bool cloudCredentialsRequired)
    {
        var shouldUseAzureStorage = GetAzureStorageConnectionString(args).HasValue();
        var shouldUseAwsS3 = args.AwsBucketName.HasValue();

        if (!cloudCredentialsRequired)
        {
            if (shouldUseAzureStorage)
            {
                _log.LogWarning("Ignoring provided Azure Blob Storage credentials because you are running GitHub Enterprise Server (GHES) 3.8.0 or later. The blob storage credentials configured in your GHES Management Console will be used instead.");
            }

            if (shouldUseAwsS3)
            {
                _log.LogWarning("Ignoring provided AWS S3 credentials because you are running GitHub Enterprise Server (GHES) 3.8.0 or later. The blob storage credentials configured in your GHES Management Console will be used instead.");
            }

            if (args.UseGithubStorage)
            {
                _log.LogWarning("Providing the --use-github-storage flag will supersede any credentials you have configured in your GitHub Enterprise Server (GHES) Management Console.");
            }

            if (args.KeepArchive)
            {
                _log.LogWarning("Ignoring --keep-archive option because there is no downloaded archive to keep");
            }

            return;
        }

        if (!shouldUseAzureStorage && !shouldUseAwsS3 && !args.UseGithubStorage)
        {
            throw new OctoshiftCliException(
                "Either Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) or " +
                "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env.variable)) or " +
                "GitHub Storage Option (--use-github-storage) " +
                "must be provided.");
        }

        if (shouldUseAzureStorage && shouldUseAwsS3)
        {
            throw new OctoshiftCliException(
                "Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) and " +
                "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env.variable)) cannot be " +
                "specified together.");
        }

        if (shouldUseAwsS3)
        {
            if (!GetAwsAccessKey(args).HasValue())
            {
                throw new OctoshiftCliException("Either --aws-access-key or AWS_ACCESS_KEY_ID environment variable must be set.");
            }

            if (!GetAwsSecretKey(args).HasValue())
            {
                throw new OctoshiftCliException("Either --aws-secret-key or AWS_SECRET_ACCESS_KEY environment variable must be set.");
            }

            if (GetAwsRegion(args).IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Either --aws-region or AWS_REGION environment variable must be set.");
            }
        }
        else if (new[] { args.AwsAccessKey, args.AwsSecretKey, args.AwsSessionToken, args.AwsRegion }.Any(x => x.HasValue()))
        {
            throw new OctoshiftCliException("The AWS S3 bucket name must be provided with --aws-bucket-name if other AWS S3 upload options are set.");
        }
    }

    private string GetAwsAccessKey(MigrateRepoCommandArgs args) => args.AwsAccessKey.HasValue() ? args.AwsAccessKey : _environmentVariableProvider.AwsAccessKeyId(false);

    private string GetAwsSecretKey(MigrateRepoCommandArgs args) => args.AwsSecretKey.HasValue() ? args.AwsSecretKey : _environmentVariableProvider.AwsSecretAccessKey(false);

    private string GetAwsRegion(MigrateRepoCommandArgs args) => args.AwsRegion.HasValue() ? args.AwsRegion : _environmentVariableProvider.AwsRegion(false);

    private string GetAzureStorageConnectionString(MigrateRepoCommandArgs args) => args.AzureStorageConnectionString.HasValue()
        ? args.AzureStorageConnectionString
        : _environmentVariableProvider.AzureStorageConnectionString(false);
}
