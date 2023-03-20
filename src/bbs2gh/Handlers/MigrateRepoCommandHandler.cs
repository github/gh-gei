﻿using System;
using System.Linq;
using System.Runtime.InteropServices;
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
        _log.RegisterSecret(args.AwsAccessKey);
        _log.RegisterSecret(args.AwsSecretKey);
        _log.RegisterSecret(args.AwsSessionToken);

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
                args.ArchivePath = GetSourceExportArchiveAbsolutePath(args.BbsSharedHome, exportId);
            }

            try
            {
                args.ArchiveUrl = args.AwsBucketName.HasValue()
                    ? await UploadArchiveToAws(args.AwsBucketName, args.ArchivePath)
                    : await UploadArchiveToAzure(args.ArchivePath);
            }
            finally
            {
                if (!args.KeepArchive && ShouldDownloadArchive(args))
                {
                    DeleteArchive(args.ArchivePath);
                }
            }
        }

        if (ShouldImportArchive(args))
        {
            await ImportArchive(args, args.ArchiveUrl);
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
        return args.ArchiveUrl.HasValue() || args.GithubOrg.HasValue();
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

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
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

        var (migrationState, _, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(CHECK_STATUS_DELAY_IN_MILLISECONDS);
            (migrationState, _, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);
        }

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-target-org {args.GithubOrg} --target-repo {args.GithubRepo}`");
            throw new OctoshiftCliException($"Migration #{migrationId} failed: {failureReason}");
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        _log.LogInformation($"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-target-org {args.GithubOrg} --target-repo {args.GithubRepo}`");
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

        LogAwsOptions(args);

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
            _log.LogInformation($"ARCHIVE DOWNLOAD HOST: {args.ArchiveDownloadHost}");
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

        if (args.SmbDomain.HasValue())
        {
            _log.LogInformation($"SMB DOMAIN: {args.SmbDomain}");
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

        if (args.KeepArchive)
        {
            _log.LogInformation("KEEP ARCHIVE: true");
        }

        if (args.NoSslVerify)
        {
            _log.LogInformation("NO SSL VERIFY: true");
        }
    }

    private void LogAwsOptions(MigrateRepoCommandArgs args)
    {
        if (args.AwsBucketName.HasValue())
        {
            _log.LogInformation($"AWS BUCKET NAME: {args.AwsBucketName}");
        }

        if (args.AwsAccessKey.HasValue())
        {
            _log.LogInformation("AWS ACCESS KEY: ********");
        }

        if (args.AwsSecretKey.HasValue())
        {
            _log.LogInformation("AWS SECRET KEY: ********");
        }

        if (args.AwsSessionToken.HasValue())
        {
            _log.LogInformation("AWS SESSION TOKEN: ********");
        }

        if (args.AwsRegion.HasValue())
        {
            _log.LogInformation($"AWS REGION: {args.AwsRegion}");
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
            if (args.Kerberos)
            {
                if (args.BbsUsername.HasValue() || args.BbsPassword.HasValue())
                {
                    throw new OctoshiftCliException("--bbs-username and --bbs-password cannot be provided with --kerberos.");
                }
            }
            else
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

            ValidateDownloadOptions(args);
        }
        else
        {
            if (args.BbsUsername.HasValue() || args.BbsPassword.HasValue())
            {
                throw new OctoshiftCliException("--bbs-username and --bbs-password can only be provided with --bbs-server-url.");
            }

            if (args.NoSslVerify)
            {
                throw new OctoshiftCliException("--no-ssl-verify can only be provided with --bbs-server-url.");
            }

            if (new[] { args.SshUser, args.SshPrivateKey, args.ArchiveDownloadHost, args.SmbUser, args.SmbPassword, args.SmbDomain }.Any(obj => obj.HasValue()))
            {
                throw new OctoshiftCliException("SSH or SMB download options can only be provided with --bbs-server-url.");
            }
        }

        if (ShouldUploadArchive(args))
        {
            ValidateUploadOptions(args);
        }

        if (ShouldImportArchive(args))
        {
            ValidateImportOptions(args);
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

        if ((args.SmbUser.HasValue() && GetSmbPassword(args).IsNullOrWhiteSpace()) || (args.SmbPassword.HasValue() && args.SmbUser.IsNullOrWhiteSpace()))
        {
            throw new OctoshiftCliException("Both --smb-user and --smb-password (or SMB_PASSWORD env. variable) must be specified for SMB download.");
        }

        if (args.ArchiveDownloadHost.HasValue() && !shouldUseSsh && !shouldUseSmb)
        {
            throw new OctoshiftCliException("--archive-download-host can only be provided if SSH or SMB download options are provided.");
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
                "AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env.variable)) " +
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
#pragma warning disable CS0618
                if (_environmentVariableProvider.AwsAccessKey(false).HasValue())
#pragma warning restore CS0618
                {
                    _log.LogWarning("AWS_ACCESS_KEY environment variable is deprecated and will be removed in future releases. Please consider using AWS_ACCESS_KEY_ID environment variable instead.");
                }
                else
                {
                    throw new OctoshiftCliException("Either --aws-access-key or AWS_ACCESS_KEY_ID environment variable must be set.");
                }
            }

            if (!GetAwsSecretKey(args).HasValue())
            {
#pragma warning disable CS0618
                if (_environmentVariableProvider.AwsSecretKey(false).HasValue())
#pragma warning restore CS0618
                {
                    _log.LogWarning("AWS_SECRET_KEY environment variable is deprecated and will be removed in future releases. Please consider using AWS_SECRET_ACCESS_KEY environment variable instead.");
                }
                else
                {
                    throw new OctoshiftCliException("Either --aws-secret-key or AWS_SECRET_ACCESS_KEY environment variable must be set.");
                }
            }

            if (GetAwsSessionToken(args).HasValue() && GetAwsRegion(args).IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException(
                    "--aws-region or AWS_REGION environment variable must be provided with --aws-session-token or AWS_SESSION_TOKEN environment variable.");
            }

            if (!GetAwsRegion(args).HasValue())
            {
                _log.LogWarning("Specifying an AWS region with the --aws-region argument or AWS_REGION environment variable is currently not required, " +
                                "but will be required in a future release. Defaulting to us-east-1.");
            }
        }
        else if (new[] { args.AwsAccessKey, args.AwsSecretKey, args.AwsSessionToken, args.AwsRegion }.Any(x => x.HasValue()))
        {
            throw new OctoshiftCliException("The AWS S3 bucket name must be provided with --aws-bucket-name if other AWS S3 upload options are set.");
        }
    }

    private void ValidateImportOptions(MigrateRepoCommandArgs args)
    {
        if (args.GithubOrg.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--github-org must be provided in order to import the Bitbucket archive.");
        }

        if (args.GithubRepo.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--github-repo must be provided in order to import the Bitbucket archive.");
        }
    }

    private string GetAwsAccessKey(MigrateRepoCommandArgs args) => args.AwsAccessKey.HasValue() ? args.AwsAccessKey : _environmentVariableProvider.AwsAccessKeyId(false);

    private string GetAwsSecretKey(MigrateRepoCommandArgs args) => args.AwsSecretKey.HasValue() ? args.AwsSecretKey : _environmentVariableProvider.AwsSecretAccessKey(false);

    private string GetAwsRegion(MigrateRepoCommandArgs args) => args.AwsRegion.HasValue() ? args.AwsRegion : _environmentVariableProvider.AwsRegion(false);

    private string GetAwsSessionToken(MigrateRepoCommandArgs args) =>
        args.AwsSessionToken.HasValue() ? args.AwsSessionToken : _environmentVariableProvider.AwsSessionToken(false);

    private string GetAzureStorageConnectionString(MigrateRepoCommandArgs args) => args.AzureStorageConnectionString.HasValue()
        ? args.AzureStorageConnectionString
        : _environmentVariableProvider.AzureStorageConnectionString(false);

    private string GetBbsUsername(MigrateRepoCommandArgs args) => args.BbsUsername.HasValue() ? args.BbsUsername : _environmentVariableProvider.BbsUsername(false);

    private string GetBbsPassword(MigrateRepoCommandArgs args) => args.BbsPassword.HasValue() ? args.BbsPassword : _environmentVariableProvider.BbsPassword(false);

    private string GetSmbPassword(MigrateRepoCommandArgs args) => args.SmbPassword.HasValue() ? args.SmbPassword : _environmentVariableProvider.SmbPassword(false);
}
