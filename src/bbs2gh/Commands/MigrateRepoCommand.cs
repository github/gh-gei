using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class MigrateRepoCommand : Command
{
    private readonly OctoLogger _log;
    private readonly GithubApiFactory _githubApiFactory;
    private readonly BbsApiFactory _bbsApiFactory;
    private readonly IAzureApiFactory _azureApiFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly FileSystemProvider _fileSystemProvider;
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000;

    public MigrateRepoCommand(
        OctoLogger log,
        GithubApiFactory githubApiFactory,
        BbsApiFactory bbsApiFactory,
        IAzureApiFactory azureApiFactory,
        EnvironmentVariableProvider environmentVariableProvider,
        FileSystemProvider fileSystemProvider
    ) : base("migrate-repo")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
        _bbsApiFactory = bbsApiFactory;
        _azureApiFactory = azureApiFactory;
        _environmentVariableProvider = environmentVariableProvider;
        _fileSystemProvider = fileSystemProvider;

        Description = "Import a Bitbucket Server archive to GitHub.";
        Description += Environment.NewLine;
        Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

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
        var githubPat = new Option<string>("--github-pat")
        {
            IsRequired = false
        };
        var wait = new Option("--wait")
        {
            Description = "Synchronously waits for the repo migration to finish."
        };
        var verbose = new Option("--verbose")
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

        _log.Verbose = args.Verbose;

        if (args.BbsServerUrl.HasValue())
        {
            await GenerateArchive(args);
        }
        else if (args.ArchivePath.HasValue())
        {
            var archiveUrl = await UploadArchive(args.AzureStorageConnectionString, args.ArchivePath);
            await ImportArchive(args, archiveUrl);

        }
        else if (args.ArchiveUrl.HasValue())
        {
            await ImportArchive(args, args.ArchiveUrl);
        }
    }

    private async Task GenerateArchive(MigrateRepoCommandArgs args)
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

        var bbsApi = _bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername, args.BbsPassword);

        _log.LogInformation($"BBS SERVER URL: {args.BbsServerUrl}...");
        _log.LogInformation($"BBS PROJECT: {args.BbsProject}");
        _log.LogInformation($"BBS REPO: {args.BbsRepo}");

        var exportId = await bbsApi.StartExport(args.BbsProject, args.BbsRepo);

        if (!args.Wait)
        {
            _log.LogInformation($"Export started. Export ID: {exportId}");
            return;
        }

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
    }

    private async Task<string> UploadArchive(string azureStorageConnectionString, string archivePath)
    {
        azureStorageConnectionString ??= _environmentVariableProvider.AzureStorageConnectionString();
        var azureApi = _azureApiFactory.Create(azureStorageConnectionString);

        var archiveData = await _fileSystemProvider.FileAsByteArray(archivePath);
        var guid = Guid.NewGuid().ToString();
        var archiveBlobUrl = await azureApi.UploadToBlob($"{guid}.tar", archiveData);

        _log.LogInformation($"Archive at: {archiveBlobUrl}");

        return archiveBlobUrl.ToString();
    }

    private async Task ImportArchive(MigrateRepoCommandArgs args, string archiveUrl = null)
    {
        _log.LogInformation("Migrating Repo...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");

        archiveUrl ??= args.ArchiveUrl;

        if (args.GithubPat is not null)
        {
            _log.LogInformation("GITHUB PAT: ***");
        }
        if (args.Wait)
        {
            _log.LogInformation("WAIT: true");
        }

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
}
