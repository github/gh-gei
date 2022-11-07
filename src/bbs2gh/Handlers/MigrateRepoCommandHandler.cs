using System;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Commands;
using OctoshiftCLI.BbsToGithub.Services;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.BbsToGithub.Handlers;

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
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000;

    public MigrateRepoCommandHandler(
        OctoLogger log,
        GithubApi githubApi,
        BbsApi bbsApi,
        EnvironmentVariableProvider environmentVariableProvider,
        IBbsArchiveDownloader bbsArchiveDownloader,
        AzureApi azureApi,
        AwsApi awsApi,
        FileSystemProvider fileSystemProvider)
    {
        _log = log;
        _githubApi = githubApi;
        _bbsApi = bbsApi;
        _azureApi = azureApi;
        _awsApi = awsApi;
        _environmentVariableProvider = environmentVariableProvider;
        _bbsArchiveDownloader = bbsArchiveDownloader;
        _fileSystemProvider = fileSystemProvider;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.RegisterSecret(args.AzureStorageConnectionString);
        _log.RegisterSecret(args.BbsPassword);
        _log.RegisterSecret(args.GithubPat);
        _log.RegisterSecret(args.SmbPassword);

        LogOptions(args);
        ValidateOptions(args);

        _log.Verbose = args.Verbose;

        var exportId = 0L;

        if (ShouldGenerateArchive(args))
        {
            exportId = await GenerateArchive(args);

            if (ShouldDownloadArchive(args))
            {
                args.ArchivePath = await DownloadArchive(exportId);
            }
        }

        if (ShouldUploadArchive(args))
        {
            // This is for the case where the CLI is being run on the BBS server itself
            if (args.ArchivePath.IsNullOrWhiteSpace())
            {
                args.ArchivePath = _bbsArchiveDownloader.GetSourceExportArchiveAbsolutePath(exportId);
            }

            args.ArchiveUrl = args.AwsBucketName.HasValue()
                ? await UploadArchiveToAws(args.AwsBucketName, args.ArchivePath)
                : await UploadArchiveToAzure(args.ArchivePath);
        }

        if (ShouldImportArchive(args))
        {
            await ImportArchive(args, args.ArchiveUrl);
        }
    }

    private bool ShouldGenerateArchive(MigrateRepoCommandArgs args)
    {
        return args.BbsServerUrl.HasValue();
    }

    private bool ShouldDownloadArchive(MigrateRepoCommandArgs args)
    {
        return args.SshUser.HasValue() || args.SmbUser.HasValue();
    }

    private bool ShouldUploadArchive(MigrateRepoCommandArgs args)
    {
        return args.ArchiveUrl.IsNullOrWhiteSpace() && args.GithubOrg.HasValue();
    }

    private bool ShouldImportArchive(MigrateRepoCommandArgs args)
    {
        return args.ArchiveUrl.HasValue();
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
            await Task.Delay(CHECK_STATUS_DELAY_IN_MILLISECONDS);
            (exportState, exportMessage, exportProgress) = await _bbsApi.GetExport(exportId);
        }

        if (ExportState.IsError(exportState))
        {
            throw new OctoshiftCliException($"Bitbucket export failed --> State: {exportState}; Message: {exportMessage}");
        }

        _log.LogInformation($"Export completed. Your migration archive should be ready on your instance at $BITBUCKET_SHARED_HOME/data/migration/export/Bitbucket_export_{exportId}.tar");

        return exportId;
    }

    private async Task<string> UploadArchiveToAzure(string archivePath)
    {
        _log.LogInformation("Uploading Archive to Azure...");

        var archiveData = await _fileSystemProvider.ReadAllBytesAsync(archivePath);
        var archiveName = GenerateArchiveName();
        var archiveBlobUrl = await _azureApi.UploadToBlob(archiveName, archiveData);

        return archiveBlobUrl.ToString();
    }

    private string GenerateArchiveName() => $"{Guid.NewGuid()}.tar";

    private async Task<string> UploadArchiveToAws(string bucketName, string archivePath)
    {
        _log.LogInformation("Uploading Archive to AWS...");

        var keyName = GenerateArchiveName();
        var archiveBlobUrl = await _awsApi.UploadToBucket(bucketName, archivePath, keyName);

        return archiveBlobUrl;
    }

    private async Task ImportArchive(MigrateRepoCommandArgs args, string archiveUrl = null)
    {
        _log.LogInformation("Importing Archive...");

        archiveUrl ??= args.ArchiveUrl;

        args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);
        var migrationSourceId = await _githubApi.CreateBbsMigrationSource(githubOrgId);

        string migrationId;

        try
        {
            migrationId = await _githubApi.StartBbsMigration(migrationSourceId, githubOrgId, args.GithubRepo, args.GithubPat, archiveUrl);
        }
        catch (OctoshiftCliException ex) when (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
        {
            _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
            return;
        }

        if (!args.Wait)
        {
            _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, failureReason) = await _githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(CHECK_STATUS_DELAY_IN_MILLISECONDS);
            (migrationState, _, failureReason) = await _githubApi.GetMigration(migrationId);
        }

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            throw new OctoshiftCliException($"Migration #{migrationId} failed: {failureReason}");
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
    }

    private void LogOptions(MigrateRepoCommandArgs args)
    {
        _log.LogInformation("Migrating repo...");

        if (args.BbsServerUrl.HasValue())
        {
            _log.LogInformation($"BBS SERVER URL: {args.BbsServerUrl}");
        }

        if (args.BbsProject.HasValue())
        {
            _log.LogInformation($"BBS PROJECT: {args.BbsProject}");
        }

        if (args.BbsRepo.HasValue())
        {
            _log.LogInformation($"BBS REPO: {args.BbsRepo}");
        }

        if (args.BbsUsername.HasValue())
        {
            _log.LogInformation($"BBS USERNAME: {args.BbsUsername}");
        }

        if (args.BbsPassword.HasValue())
        {
            _log.LogInformation("BBS PASSWORD: ********");
        }

        if (args.ArchiveUrl.HasValue())
        {
            _log.LogInformation($"ARCHIVE URL: {args.ArchiveUrl}");
        }

        if (args.ArchivePath.HasValue())
        {
            _log.LogInformation($"ARCHIVE PATH: {args.ArchivePath}");
        }

        if (args.AzureStorageConnectionString.HasValue())
        {
            _log.LogInformation($"AZURE STORAGE CONNECTION STRING: ********");
        }

        if (args.AwsBucketName.HasValue())
        {
            _log.LogInformation($"AWS BUCKET NAME: {args.AwsBucketName}");
        }

        if (args.AwsAccessKey.HasValue())
        {
            _log.LogInformation($"AWS ACCESS KEY: ********");
        }

        if (args.AwsSecretKey.HasValue())
        {
            _log.LogInformation($"AWS SECRET KEY: ********");
        }

        if (args.GithubOrg.HasValue())
        {
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        }

        if (args.GithubRepo.HasValue())
        {
            _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
        }

        if (args.SshUser.HasValue())
        {
            _log.LogInformation($"SSH USER: {args.SshUser}");
        }

        if (args.SshPrivateKey.HasValue())
        {
            _log.LogInformation($"SSH PRIVATE KEY: {args.SshPrivateKey}");
        }

        if (args.SshPort.HasValue())
        {
            _log.LogInformation($"SSH PORT: {args.SshPort}");
        }

        if (args.SmbUser.HasValue())
        {
            _log.LogInformation($"SMB USER: {args.SmbUser}");
        }

        if (args.SmbPassword.HasValue())
        {
            _log.LogInformation($"SMB PASSWORD: ********");
        }

        if (args.GithubPat.HasValue())
        {
            _log.LogInformation($"GITHUB PAT: ********");
        }

        if (args.Wait)
        {
            _log.LogInformation("WAIT: true");
        }

        if (args.BbsSharedHome.HasValue())
        {
            _log.LogInformation($"SHARED HOME: {args.BbsSharedHome}");
        }
    }

    private void ValidateOptions(MigrateRepoCommandArgs args)
    {
        if (!args.BbsServerUrl.HasValue() && !args.ArchiveUrl.HasValue() && !args.ArchivePath.HasValue())
        {
            throw new OctoshiftCliException("Either --bbs-server-url, --archive-path, or --archive-url must be specified.");
        }

        if (args.BbsServerUrl.HasValue() && args.ArchiveUrl.HasValue())
        {
            throw new OctoshiftCliException("Only one of --bbs-server-url or --archive-url can be specified.");
        }

        if (args.BbsServerUrl.HasValue() && args.ArchivePath.HasValue())
        {
            throw new OctoshiftCliException("Only one of --bbs-server-url or --archive-path can be specified.");
        }

        if (args.ArchivePath.HasValue() && args.ArchiveUrl.HasValue())
        {
            throw new OctoshiftCliException("Only one of --archive-path or --archive-url can be specified.");
        }

        if (ShouldGenerateArchive(args))
        {
            if (GetBbsUsername(args).IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("BBS username must be either set as BBS_USERNAME environment variable or passed as --bbs-username.");
            }

            if (GetBbsPassword(args).IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("BBS password must be either set as BBS_PASSWORD environment variable or passed as --bbs-password.");
            }

            if (ShouldDownloadArchive(args))
            {
                ValidateDownloadOptions(args);
            }
        }
        else
        {
            if (args.BbsUsername.HasValue() || args.BbsPassword.HasValue())
            {
                throw new OctoshiftCliException("--bbs-username and --bbs-password can only be provided with --bbs-server-url.");
            }

            if (new[] { args.SshUser, args.SshPrivateKey, args.SmbUser, args.SmbPassword }.Any(obj => obj.HasValue()))
            {
                throw new OctoshiftCliException("SSH or SMB download options can only be provided with --bbs-server-url.");
            }
        }

        if (ShouldUploadArchive(args))
        {
            ValidateUploadOptions(args);
        }
    }

    private void ValidateDownloadOptions(MigrateRepoCommandArgs args)
    {
        var sshArgs = new[] { args.SshUser, args.SshPrivateKey };
        var smbArgs = new[] { args.SmbUser, args.SmbPassword };
        var shouldUseSsh = sshArgs.Any(arg => arg.HasValue());
        var shouldUseSmb = smbArgs.Any(arg => arg.HasValue());

        if (shouldUseSsh && shouldUseSmb)
        {
            throw new OctoshiftCliException("You can't provide both SSH and SMB credentials together.");
        }

        if (args.SshUser.HasValue() ^ args.SshPrivateKey.HasValue())
        {
            throw new OctoshiftCliException("Both --ssh-user and --ssh-private-key must be specified for SSH download.");
        }

        if (args.SmbUser.HasValue() ^ args.SmbPassword.HasValue())
        {
            throw new OctoshiftCliException("Both --smb-user and --smb-password must be specified for SMB download.");
        }
    }

    private void ValidateUploadOptions(MigrateRepoCommandArgs args)
    {
        var shouldUseAzureStorage = GetAzureStorageConnectionString(args).HasValue();
        var shouldUseAwsS3 = args.AwsBucketName.HasValue();
        if (!shouldUseAzureStorage && !shouldUseAwsS3)
        {
            throw new OctoshiftCliException(
                "Either Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) or " +
                "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY env. variable), --aws-secret-key (or AWS_SECRET_Key env.variable)) " +
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

    private string GetAwsAccessKey(MigrateRepoCommandArgs args) => args.AwsAccessKey.HasValue() ? args.AwsAccessKey : _environmentVariableProvider.AwsAccessKey(false);

    private string GetAwsSecretKey(MigrateRepoCommandArgs args) => args.AwsSecretKey.HasValue() ? args.AwsSecretKey : _environmentVariableProvider.AwsSecretKey(false);

    private string GetAzureStorageConnectionString(MigrateRepoCommandArgs args) => args.AzureStorageConnectionString.HasValue()
        ? args.AzureStorageConnectionString
        : _environmentVariableProvider.AzureStorageConnectionString(false);

    private string GetBbsUsername(MigrateRepoCommandArgs args) => args.BbsUsername.HasValue() ? args.BbsUsername : _environmentVariableProvider.BbsUsername(false);

    private string GetBbsPassword(MigrateRepoCommandArgs args) => args.BbsPassword.HasValue() ? args.BbsPassword : _environmentVariableProvider.BbsPassword(false);
}
