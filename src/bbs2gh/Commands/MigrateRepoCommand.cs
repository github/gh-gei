using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class MigrateRepoCommand : Command
{
    private readonly OctoLogger _log;
    private readonly GithubApiFactory _githubApiFactory;
    private readonly BbsApiFactory _bbsApiFactory;
    private readonly IAzureApiFactory _azureApiFactory;
    private readonly IAwsApiFactory _awsApiFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly BbsArchiveDownloaderFactory _bbsArchiveDownloaderFactory;
    private readonly FileSystemProvider _fileSystemProvider;
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000;

    public MigrateRepoCommand(
        OctoLogger log,
        GithubApiFactory githubApiFactory,
        BbsApiFactory bbsApiFactory,
        EnvironmentVariableProvider environmentVariableProvider,
        BbsArchiveDownloaderFactory bbsArchiveDownloaderFactory,
        IAzureApiFactory azureApiFactory,
        IAwsApiFactory awsApiFactory,
        FileSystemProvider fileSystemProvider) : base(
            name: "migrate-repo",
            description: "Import a Bitbucket Server archive to GitHub." +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
        _bbsApiFactory = bbsApiFactory;
        _azureApiFactory = azureApiFactory;
        _awsApiFactory = awsApiFactory;
        _environmentVariableProvider = environmentVariableProvider;
        _bbsArchiveDownloaderFactory = bbsArchiveDownloaderFactory;
        _fileSystemProvider = fileSystemProvider;

        // Arguments to generate a new archive
        var bbsServerUrl = new Option<string>("--bbs-server-url")
        {
            IsRequired = false,
            Description = "The full URL of the Bitbucket Server/Data Center to migrate from. E.g. http://bitbucket.contoso.com:7990"
        };

        var bbsProject = new Option<string>("--bbs-project")
        {
            IsRequired = false,
            Description = "The Bitbucket project to migrate."
        };

        var bbsRepo = new Option<string>("--bbs-repo")
        {
            IsRequired = false,
            Description = "The Bitbucket repository to migrate."
        };

        var bbsUsername = new Option<string>("--bbs-username")
        {
            IsRequired = false,
            Description = "The Bitbucket username of a user with site admin privileges. If not set will be read from BBS_USERNAME environment variable."
        };

        var bbsPassword = new Option<string>("--bbs-password")
        {
            IsRequired = false,
            Description = "The Bitbucket password of the user specified by --bbs-username. If not set will be read from BBS_PASSWORD environment variable."
        };

        // Arguments to import an existing archive
        var archiveUrl = new Option<string>("--archive-url")
        {
            IsRequired = false,
            Description = "URL used to download Bitbucket Server migration archive. Only needed if you want to manually retrieve the archive from BBS instead of letting this CLI do that for you."
        };

        var archivePath = new Option<string>("--archive-path")
        {
            IsRequired = false,
            Description = "Path to Bitbucket Server migration archive on disk."
        };

        var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
        {
            IsRequired = false,
            Description = "A connection string for an Azure Storage account, used to upload the BBS archive."
        };

        var githubOrg = new Option<string>("--github-org")
        {
            IsRequired = false
        };
        var githubRepo = new Option<string>("--github-repo")
        {
            IsRequired = false
        };

        var sshUser = new Option<string>("--ssh-user")
        {
            IsRequired = false,
            Description = "The SSH user to be used for downloading the export archive off of the Bitbucket server."
        };
        var sshPrivateKey = new Option<string>("--ssh-private-key")
        {
            IsRequired = false,
            Description = "The full path of the private key file to be used for downloading the export archive off of the Bitbucket Server using SSH/SFTP." +
                          Environment.NewLine +
                          "Supported private key formats:" +
                          Environment.NewLine +
                          "  - RSA in OpenSSL PEM format." +
                          Environment.NewLine +
                          "  - DSA in OpenSSL PEM format." +
                          Environment.NewLine +
                          "  - ECDSA 256/384/521 in OpenSSL PEM format." +
                          Environment.NewLine +
                          "  - ECDSA 256/384/521, ED25519 and RSA in OpenSSH key format."
        };
        var sshPort = new Option<int>("--ssh-port")
        {
            IsRequired = false,
            Description = "The SSH port (default: 22)."
        };

        var smbUser = new Option<string>("--smb-user")
        {
            IsRequired = false,
            IsHidden = true,
            Description = "The SMB user to be used for downloading the export archive off of the Bitbucket server."
        };
        var smbPassword = new Option<string>("--smb-password")
        {
            IsRequired = false,
            IsHidden = true,
            Description = "The SMB password to be used for downloading the export archive off of the Bitbucket server."
        };

        var githubPat = new Option<string>("--github-pat")
        {
            IsRequired = false
        };
        var wait = new Option<bool>("--wait")
        {
            Description = "Synchronously waits for the repo migration to finish."
        };
        var verbose = new Option<bool>("--verbose")
        {
            IsRequired = false
        };

        AddOption(archiveUrl);
        AddOption(githubOrg);
        AddOption(githubRepo);
        AddOption(githubPat);

        AddOption(bbsServerUrl);
        AddOption(bbsProject);
        AddOption(bbsRepo);
        AddOption(bbsUsername);
        AddOption(bbsPassword);

        AddOption(sshUser);
        AddOption(sshPrivateKey);
        AddOption(sshPort);

        AddOption(smbUser);
        AddOption(smbPassword);

        AddOption(archivePath);
        AddOption(azureStorageConnectionString);

        AddOption(wait);
        AddOption(verbose);

        Handler = CommandHandler.Create<MigrateRepoCommandArgs>(Invoke);
    }

    public async Task Invoke(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

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
            args.ArchivePath = await DownloadArchive(exportId, args);
        }

        if (args.ArchivePath.HasValue())
        {
            args.ArchiveUrl = await UploadArchive(args.AzureStorageConnectionString, args.ArchivePath);
        }

        if (args.ArchiveUrl.HasValue())
        {
            await ImportArchive(args, args.ArchiveUrl);
        }
    }

    private async Task<string> DownloadArchive(long exportId, MigrateRepoCommandArgs args)
    {
        var downloader = args.SshUser.HasValue()
            ? _bbsArchiveDownloaderFactory.CreateSshDownloader(ExtractHost(args.BbsServerUrl), args.SshUser, args.SshPrivateKey, args.SshPort)
            : _bbsArchiveDownloaderFactory.CreateSmbDownloader();

        _log.LogInformation($"Download archive {exportId} started...");
        var downloadedArchiveFullPath = await downloader.Download(exportId);
        _log.LogInformation($"Archive was successfully downloaded at \"{downloadedArchiveFullPath}\".");

        return downloadedArchiveFullPath;
    }

    private string ExtractHost(string bbsServerUrl) => new Uri(bbsServerUrl).Host;

    private async Task<long> GenerateArchive(MigrateRepoCommandArgs args)
    {
        var bbsApi = _bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername, args.BbsPassword);

        var exportId = await bbsApi.StartExport(args.BbsProject, args.BbsRepo);

        _log.LogInformation($"Export started. Export ID: {exportId}");

        var (exportState, exportMessage, exportProgress) = await bbsApi.GetExport(exportId);

        while (ExportState.IsInProgress(exportState))
        {
            _log.LogInformation($"Export status: {exportState}; {exportProgress}% complete");
            await Task.Delay(CHECK_STATUS_DELAY_IN_MILLISECONDS);
            (exportState, exportMessage, exportProgress) = await bbsApi.GetExport(exportId);
        }

        if (ExportState.IsError(exportState))
        {
            throw new OctoshiftCliException($"Bitbucket export failed --> State: {exportState}; Message: {exportMessage}");
        }

        _log.LogInformation($"Export completed. Your migration archive should be ready on your instance at $BITBUCKET_SHARED_HOME/data/migration/export/Bitbucket_export_{exportId}.tar");

        return exportId;
    }

    private async Task<string> UploadArchive(string azureStorageConnectionString, string archivePath)
    {
        _log.LogInformation("Uploading Archive...");

        azureStorageConnectionString ??= _environmentVariableProvider.AzureStorageConnectionString();
        var azureApi = _azureApiFactory.Create(azureStorageConnectionString);

        var archiveData = await _fileSystemProvider.ReadAllBytesAsync(archivePath);
        var guid = Guid.NewGuid().ToString();
        var archiveBlobUrl = await azureApi.UploadToBlob($"{guid}.tar", archiveData);

        return archiveBlobUrl.ToString();
    }

    private async Task ImportArchive(MigrateRepoCommandArgs args, string archiveUrl = null)
    {
        _log.LogInformation("Importing Archive...");

        archiveUrl ??= args.ArchiveUrl;

        args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);
        var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
        var migrationSourceId = await githubApi.CreateBbsMigrationSource(githubOrgId);

        string migrationId;

        try
        {
            migrationId = await githubApi.StartBbsMigration(migrationSourceId, githubOrgId, args.GithubRepo, args.GithubPat, archiveUrl);
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

        var (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
            await Task.Delay(CHECK_STATUS_DELAY_IN_MILLISECONDS);
            (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);
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
            _log.LogInformation($"AZURE STORAGE CONNECTION STRING: {args.AzureStorageConnectionString}");
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

public class MigrateRepoCommandArgs
{
    public string ArchiveUrl { get; set; }
    public string ArchivePath { get; set; }

    public string AzureStorageConnectionString { get; set; }

    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string GithubPat { get; set; }
    public bool Wait { get; set; }
    public bool Verbose { get; set; }

    public string BbsServerUrl { get; set; }
    public string BbsProject { get; set; }
    public string BbsRepo { get; set; }
    public string BbsUsername { get; set; }
    public string BbsPassword { get; set; }

    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public int SshPort { get; set; } = 22;

    public string SmbUser { get; set; }
    public string SmbPassword { get; set; }
}
