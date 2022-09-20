using System;
using System.CommandLine;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using Polly;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands;

public class DownloadLogsCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;
    private readonly HttpDownloadService _httpDownloadService;
    private readonly RetryPolicy _retryPolicy;

    internal Func<string, bool> FileExists = path => File.Exists(path);

    public DownloadLogsCommandBase(
        OctoLogger log,
        ITargetGithubApiFactory githubApiFactory,
        HttpDownloadService httpDownloadService,
        RetryPolicy retryPolicy) : base(name: "download-logs", description: "Downloads migration logs for migrations.")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
        _httpDownloadService = httpDownloadService;
        _retryPolicy = retryPolicy;
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "GitHub organization to download logs from."
    };

    protected virtual Option<string> GithubRepo { get; } = new("--github-repo")
    {
        IsRequired = true,
        Description = "Target repository to download latest log for."
    };

    protected virtual Option<string> GithubApiUrl { get; } = new("--github-api-url")
    {
        IsRequired = false,
        Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
    };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        IsRequired = false,
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    protected virtual Option<string> MigrationLogFile { get; } = new("--migration-log-file")
    {
        IsRequired = false,
        Description = "Local file to write migration log to (default: migration-log-ORG-REPO.log)."
    };

    protected virtual Option<bool> Overwrite { get; } = new("--overwrite")
    {
        IsRequired = false,
        Description = "Overwrite migration log file if it exists."
    };

    protected virtual Option<bool> Verbose { get; } = new("--verbose")
    {
        IsRequired = false,
        Description = "Display more information to the console."
    };

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(GithubRepo);
        AddOption(GithubApiUrl);
        AddOption(GithubPat);
        AddOption(MigrationLogFile);
        AddOption(Overwrite);
        AddOption(Verbose);
    }

    public async Task Handle(DownloadLogsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogWarning("Migration logs are only available for 24 hours after a migration finishes!");

        _log.LogInformation("Downloading migration logs...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {args.GithubOrg}");
        _log.LogInformation($"{GithubRepo.GetLogFriendlyName()}: {args.GithubRepo}");

        if (args.GithubApiUrl.HasValue())
        {
            _log.LogInformation($"{GithubApiUrl.GetLogFriendlyName()}: {args.GithubApiUrl}");
        }

        if (args.GithubPat.HasValue())
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        if (args.MigrationLogFile.HasValue())
        {
            _log.LogInformation($"{MigrationLogFile.GetLogFriendlyName()}: {args.MigrationLogFile}");
        }

        args.MigrationLogFile ??= $"migration-log-{args.GithubOrg}-{args.GithubRepo}.log";

        if (FileExists(args.MigrationLogFile))
        {
            if (!args.Overwrite)
            {
                throw new OctoshiftCliException($"File {args.MigrationLogFile} already exists!  Use --overwrite to overwrite this file.");
            }

            _log.LogWarning($"Overwriting {args.MigrationLogFile} due to --overwrite option.");
        }

        var githubApi = _githubApiFactory.Create(args.GithubApiUrl, args.GithubPat);

        var result = await _retryPolicy.RetryOnResult(async () => await githubApi.GetMigrationLogUrl(args.GithubOrg, args.GithubRepo), string.Empty,
            "Waiting for migration log to populate...");

        if (result.Outcome == OutcomeType.Successful && result.Result is null)
        {
            throw new OctoshiftCliException($"Migration for repository {args.GithubRepo} not found!");
        }

        if (result.Outcome == OutcomeType.Failure)
        {
            throw new OctoshiftCliException($"Migration log for repository {args.GithubRepo} unavailable!");
        }

        var logUrl = result.Result;

        _log.LogInformation($"Downloading log for repository {args.GithubRepo} to {args.MigrationLogFile}...");
        await _httpDownloadService.Download(logUrl, args.MigrationLogFile);

        _log.LogSuccess($"Downloaded {args.GithubRepo} log to {args.MigrationLogFile}.");
    }
}

public class DownloadLogsCommandArgs
{
    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string GithubApiUrl { get; set; }
    public string GithubPat { get; set; }
    public string MigrationLogFile { get; set; }
    public bool Overwrite { get; set; }
    public bool Verbose { get; set; }
}
