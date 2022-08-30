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
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000;

    public MigrateRepoCommand(
        OctoLogger log,
        GithubApiFactory githubApiFactory,
        BbsApiFactory bbsApiFactory,
        EnvironmentVariableProvider environmentVariableProvider
    ) : base("migrate-repo")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
        _bbsApiFactory = bbsApiFactory;
        _environmentVariableProvider = environmentVariableProvider;

        Description = "Import a Bitbucket Server archive to GitHub.";
        Description += Environment.NewLine;
        Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

        // Arguments to generate a new archive
        var bbsServerUrl = new Option<string>("--bbs-server-url")
        {
            IsRequired = false,
            Description = "The full URL of the Bitbucket Server/Data Center to migrate from."
        };

        var bbsProject = new Option<string>("--bbs-project")
        {
            IsRequired = false,
            Description = "The Bitbucket project to import; defaults to '*' (all projects)."
        };

        var bbsRepo = new Option<string>("--bbs-repo")
        {
            IsRequired = false,
            Description = "The Bitbucket repository to import; defaults to '*' (all repositories)."
        };

        var bbsUsername = new Option<string>("--bbs-username")
        {
            IsRequired = false,
            Description = "The Bitbucket username of a user with project admin privileges."
        };

        var bbsPassword = new Option<string>("--bbs-password")
        {
            IsRequired = false,
            Description = "The Bitbucket password of the user specified by --bbs-username."
        };

        // Arguments to import an existing archive
        var archiveUrl = new Option<string>("--archive-url")
        {
            IsRequired = false,
            Description = "URL used to download Bitbucket Server migration archive."
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

        _log.Verbose = args.Verbose;

        if (args.BbsServerUrl.HasValue())
        {
            await GenerateArchive(args);
        }
        else if (args.ArchiveUrl.HasValue())
        {
            await ImportArchive(args);
        }
        else
        {
            throw new ArgumentException("Either --bbs-server-url or --archive-url must be specified.");
        }
    }

    private async Task GenerateArchive(MigrateRepoCommandArgs args)
    {
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

    private async Task ImportArchive(MigrateRepoCommandArgs args)
    {
        _log.LogInformation("Migrating Repo...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");

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
            migrationId = await githubApi.StartBbsMigration(migrationSourceId, githubOrgId, args.GithubRepo, args.GithubPat, args.ArchiveUrl);
        }
        catch (OctoshiftCliException ex)
        {
            if (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
            {
                _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
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
            throw new OctoshiftCliException($"Migration #{migrationId} failed: {failureReason}");
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");

    }
}

public class MigrateRepoCommandArgs
{
    public string ArchiveUrl { get; set; }
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
