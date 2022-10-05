using System;
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
        FileSystemProvider fileSystemProvider)
    {
        _log = log;
        _githubApi = githubApi;
        _bbsApi = bbsApi;
        _azureApi = azureApi;
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

        if (args.BbsServerUrl.HasValue())
        {
            exportId = await GenerateArchive(args);
        }

        if (args.SshUser.HasValue())
        {
            args.ArchivePath = await DownloadArchive(exportId);
        }

        if (args.ArchivePath.HasValue())
        {
            args.ArchiveUrl = await UploadArchive(args.ArchivePath);
        }

        if (args.ArchiveUrl.HasValue())
        {
            await ImportArchive(args, args.ArchiveUrl);
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

    private async Task<string> UploadArchive(string archivePath)
    {
        _log.LogInformation("Uploading Archive...");

        var archiveData = await _fileSystemProvider.ReadAllBytesAsync(archivePath);
        var guid = Guid.NewGuid().ToString();
        var archiveBlobUrl = await _azureApi.UploadToBlob($"{guid}.tar", archiveData);

        return archiveBlobUrl.ToString();
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

        if (args.ArchivePath.HasValue() && args.ArchiveUrl.HasValue())
        {
            throw new OctoshiftCliException("Only one of --archive-path or --archive-url can be specified.");
        }

        if (args.BbsServerUrl.HasValue())
        {
            args.BbsUsername ??= _environmentVariableProvider.BbsUsername();
            args.BbsPassword ??= _environmentVariableProvider.BbsPassword();

            if (!args.BbsUsername.HasValue())
            {
                throw new OctoshiftCliException("BBS username must be either set as BBS_USERNAME environment variable or passed as --bbs-username.");
            }

            if (!args.BbsPassword.HasValue())
            {
                throw new OctoshiftCliException("BBS password must be either set as BBS_PASSWORD environment variable or passed as --bbs-password.");
            }

            if (args.SshUser.IsNullOrWhiteSpace() && args.SmbUser.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Either --ssh-user or --smb-user must be specified.");
            }

            if (args.SshUser.HasValue() && args.SmbUser.HasValue())
            {
                throw new OctoshiftCliException("Only one of --ssh-user or --smb-user can be specified.");
            }

            if (args.SshUser.HasValue())
            {
                if (args.SshPrivateKey.IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("--ssh-private-key must be specified for SSH download.");
                }
            }
            else
            {
                if (args.SmbPassword.IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("--smb-password must be specified.");
                }
            }
        }
    }
}
