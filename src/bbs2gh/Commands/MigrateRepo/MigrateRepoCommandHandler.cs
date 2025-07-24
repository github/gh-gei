using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Services;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private readonly BbsApi _bbsApi;
    private readonly AzureApi _azureApi;
    private readonly AwsApi _awsApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly IBbsArchiveDownloader _bbsArchiveDownloader;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly WarningsCountLogger _warningsCountLogger;
    private const int CHECK_EXPORT_STATUS_DELAY_IN_MILLISECONDS = 10000;
    private const int CHECK_MIGRATION_STATUS_DELAY_IN_MILLISECONDS = 60000;

    public MigrateRepoCommandHandler(
        OctoLogger log,
        GithubApi githubApi,
        BbsApi bbsApi,
        EnvironmentVariableProvider environmentVariableProvider,
        IBbsArchiveDownloader bbsArchiveDownloader,
        AzureApi azureApi,
        AwsApi awsApi,
        FileSystemProvider fileSystemProvider,
        WarningsCountLogger warningsCountLogger)
    {
        _log = log;
        _githubApi = githubApi;
        _bbsApi = bbsApi;
        _azureApi = azureApi;
        _awsApi = awsApi;
        _environmentVariableProvider = environmentVariableProvider;
        _bbsArchiveDownloader = bbsArchiveDownloader;
        _fileSystemProvider = fileSystemProvider;
        _warningsCountLogger = warningsCountLogger;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        ValidateOptions(args);

        var exportId = 0L;
        var migrationSourceId = "";

        if (args.ShouldImportArchive())
        {
            var targetRepoExists = await _githubApi.DoesRepoExist(args.GithubOrg, args.GithubRepo);

            if (targetRepoExists)
            {
                throw new OctoshiftCliException($"A repository called {args.GithubOrg}/{args.GithubRepo} already exists");
            }

            migrationSourceId = await CreateMigrationSource(args);
        }

        if (args.ShouldGenerateArchive())
        {
            exportId = await GenerateArchive(args);

            if (args.ShouldDownloadArchive())
            {
                args.ArchivePath = await DownloadArchive(exportId);
            }

            if (!args.ShouldDownloadArchive() && args.ShouldUploadArchive())
            {
                _log.LogWarning($"You haven't specified --ssh-user or --smb-user, so we assume that you're running the CLI on the Bitbucket instance itself. If you are not running this command on the Bitbucket instance, run this command again with the --ssh-user or --smb-user argument to allow the CLI to download the migration archive from the server.");
            }
        }

        if (args.ShouldUploadArchive())
        {
            // This is for the case where the CLI is being run on the BBS server itself
            if (args.ArchivePath.IsNullOrWhiteSpace())
            {
                args.ArchivePath = GetSourceExportArchiveAbsolutePath(args.BbsSharedHome, exportId);
            }

            try
            {
                if (args.UseGithubStorage)
                {
                    args.ArchiveUrl = await UploadArchiveToGithub(args.GithubOrg, args.ArchivePath);
                }
#pragma warning disable IDE0045
                else if (args.AwsBucketName.HasValue())
#pragma warning restore IDE0045
                {
                    args.ArchiveUrl = await UploadArchiveToAws(args.AwsBucketName, args.ArchivePath);
                }
                else
                {
                    args.ArchiveUrl = await UploadArchiveToAzure(args.ArchivePath);
                }

            }
            finally
            {
                if (!args.KeepArchive && args.ShouldDownloadArchive())
                {
                    DeleteArchive(args.ArchivePath);
                }
            }
        }

        if (args.ShouldImportArchive())
        {
            await ImportArchive(args, migrationSourceId, args.ArchiveUrl);
        }
    }

    private string GetSourceExportArchiveAbsolutePath(string bbsSharedHomeDirectory, long exportId)
    {
        if (bbsSharedHomeDirectory.IsNullOrWhiteSpace())
        {
            bbsSharedHomeDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_WINDOWS
                : BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_LINUX;
        }

        return IBbsArchiveDownloader.GetSourceExportArchiveAbsolutePath(bbsSharedHomeDirectory, exportId);
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
            _log.LogWarning($"Couldn't delete the downloaded archive. Error message: \"{ex.Message}\"");
            _log.LogVerbose(ex.ToString());
        }
    }

    private async Task<string> DownloadArchive(long exportId)
    {
        _log.LogInformation($"Download archive {exportId} started...");
        var downloadedArchiveFullPath = await _bbsArchiveDownloader.Download(exportId);
        _log.LogInformation($"Archive was successfully downloaded at \"{downloadedArchiveFullPath}\".");

        return downloadedArchiveFullPath;
    }

    private async Task<long> GenerateArchive(MigrateRepoCommandArgs args)
    {
        var exportId = await _bbsApi.StartExport(args.BbsProject, args.BbsRepo);

        _log.LogInformation($"Export started. Export ID: {exportId}");

        var (exportState, exportMessage, exportProgress) = await _bbsApi.GetExport(exportId);

        while (ExportState.IsInProgress(exportState))
        {
            _log.LogInformation($"Export status: {exportState}; {exportProgress}% complete");
            await Task.Delay(CHECK_EXPORT_STATUS_DELAY_IN_MILLISECONDS);
            (exportState, exportMessage, exportProgress) = await _bbsApi.GetExport(exportId);
        }

        if (ExportState.IsError(exportState))
        {
            throw new OctoshiftCliException($"Bitbucket export failed --> State: {exportState}; Message: {exportMessage}");
        }

        _log.LogInformation($"Export completed. Your migration archive should be ready **on your Bitbucket instance** at $BITBUCKET_SHARED_HOME/data/migration/export/Bitbucket_export_{exportId}.tar");

        return exportId;
    }

    private async Task<string> UploadArchiveToAzure(string archivePath)
    {
        _log.LogInformation("Uploading Archive to Azure...");

#pragma warning disable IDE0063
        await using (var archiveData = _fileSystemProvider.OpenRead(archivePath))
#pragma warning restore IDE0063
        {
            var archiveName = GenerateArchiveName();
            var archiveBlobUrl = await _azureApi.UploadToBlob(archiveName, archiveData);
            return archiveBlobUrl.ToString();
        }
    }

    private string GenerateArchiveName() => $"{Guid.NewGuid()}.tar";

    private async Task<string> UploadArchiveToAws(string bucketName, string archivePath)
    {
        _log.LogInformation("Uploading Archive to AWS...");

        var keyName = GenerateArchiveName();
        var archiveBlobUrl = await _awsApi.UploadToBucket(bucketName, archivePath, keyName);

        return archiveBlobUrl;
    }

    private async Task<string> UploadArchiveToGithub(string org, string archivePath)
    {
        await using var archiveData = _fileSystemProvider.OpenRead(archivePath);
        var githubOrgDatabaseId = await _githubApi.GetOrganizationDatabaseId(org);

        _log.LogInformation("Uploading archive to GitHub Storage");
        var keyName = GenerateArchiveName();
        var authenticatedGitArchiveUri = await _githubApi.UploadArchiveToGithubStorage(githubOrgDatabaseId, keyName, archiveData);

        return authenticatedGitArchiveUri;
    }

    private async Task<string> CreateMigrationSource(MigrateRepoCommandArgs args)
    {
        _log.LogInformation("Creating Migration Source...");

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);

        try
        {
            return await _githubApi.CreateBbsMigrationSource(githubOrgId);
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
        {
            var insufficientPermissionsMessage = InsufficientPermissionsMessageGenerator.Generate(args.GithubOrg);
            var message = $"{ex.Message}{insufficientPermissionsMessage}";
            throw new OctoshiftCliException(message, ex);
        }
    }

    private async Task ImportArchive(MigrateRepoCommandArgs args, string migrationSourceId, string archiveUrl = null)
    {
        _log.LogInformation("Importing Archive...");

        archiveUrl ??= args.ArchiveUrl;

        var bbsRepoUrl = GetBbsRepoUrl(args);

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);

        string migrationId;

        try
        {
            migrationId = await _githubApi.StartBbsMigration(migrationSourceId, bbsRepoUrl, githubOrgId, args.GithubRepo, args.GithubPat, archiveUrl, args.TargetRepoVisibility);
        }
        catch (OctoshiftCliException ex) when (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
        {
            _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
            return;
        }

        if (args.QueueOnly)
        {
            _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 60 seconds...");
            await Task.Delay(CHECK_MIGRATION_STATUS_DELAY_IN_MILLISECONDS);
            (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);
        }

        var migrationLogAvailableMessage = $"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-org {args.GithubOrg} --github-repo {args.GithubRepo}`";

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            _log.LogError($"Migration Failed. Migration ID: {migrationId}");
            _warningsCountLogger.LogWarningsCount(warningsCount);
            _log.LogInformation(migrationLogAvailableMessage);
            throw new OctoshiftCliException(failureReason);
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        _warningsCountLogger.LogWarningsCount(warningsCount);
        _log.LogInformation(migrationLogAvailableMessage);
    }

    private string GetAwsAccessKey(MigrateRepoCommandArgs args) => args.AwsAccessKey.HasValue() ? args.AwsAccessKey : _environmentVariableProvider.AwsAccessKeyId(false);

    private string GetAwsSecretKey(MigrateRepoCommandArgs args) => args.AwsSecretKey.HasValue() ? args.AwsSecretKey : _environmentVariableProvider.AwsSecretAccessKey(false);

    private string GetAwsRegion(MigrateRepoCommandArgs args) => args.AwsRegion.HasValue() ? args.AwsRegion : _environmentVariableProvider.AwsRegion(false);

    private string GetAzureStorageConnectionString(MigrateRepoCommandArgs args) => args.AzureStorageConnectionString.HasValue()
        ? args.AzureStorageConnectionString
        : _environmentVariableProvider.AzureStorageConnectionString(false);

    private string GetBbsUsername(MigrateRepoCommandArgs args) => args.BbsUsername.HasValue() ? args.BbsUsername : _environmentVariableProvider.BbsUsername(false);

    private string GetBbsPassword(MigrateRepoCommandArgs args) => args.BbsPassword.HasValue() ? args.BbsPassword : _environmentVariableProvider.BbsPassword(false);

    private string GetSmbPassword(MigrateRepoCommandArgs args) => args.SmbPassword.HasValue() ? args.SmbPassword : _environmentVariableProvider.SmbPassword(false);

    private string GetBbsRepoUrl(MigrateRepoCommandArgs args)
    {
        return args.BbsServerUrl.HasValue() && args.BbsProject.HasValue() && args.BbsRepo.HasValue()
            ? $"{args.BbsServerUrl.TrimEnd('/')}/projects/{args.BbsProject}/repos/{args.BbsRepo}/browse"
            : "https://not-used";
    }

    private void ValidateOptions(MigrateRepoCommandArgs args)
    {
        if (args.ShouldGenerateArchive())
        {
            if (!args.Kerberos)
            {
                if (GetBbsUsername(args).IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("BBS username must be either set as BBS_USERNAME environment variable or passed as --bbs-username.");
                }

                if (GetBbsPassword(args).IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("BBS password must be either set as BBS_PASSWORD environment variable or passed as --bbs-password.");
                }
            }

            if ((args.SmbUser.HasValue() && GetSmbPassword(args).IsNullOrWhiteSpace()) || (args.SmbPassword.HasValue() && args.SmbUser.IsNullOrWhiteSpace()))
            {
                throw new OctoshiftCliException("Both --smb-user and --smb-password (or SMB_PASSWORD env. variable) must be specified for SMB download.");
            }
        }

        if (args.ShouldUploadArchive())
        {
            ValidateUploadOptions(args);
        }
    }

    private void ValidateUploadOptions(MigrateRepoCommandArgs args)
    {
        var shouldUseAzureStorage = GetAzureStorageConnectionString(args).HasValue();
        var shouldUseAwsS3 = args.AwsBucketName.HasValue();
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
    }
}
