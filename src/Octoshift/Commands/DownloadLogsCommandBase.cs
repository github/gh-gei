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

    public async Task Handle(
        string githubOrg,
        string githubRepo,
        string githubApiUrl = null,
        string githubPat = null,
        string migrationLogFile = null,
        bool overwrite = false,
        bool verbose = false
    )
    {
        _log.Verbose = verbose;

        _log.LogWarning("Migration logs are only available for 24 hours after a migration finishes!");

        _log.LogInformation("Downloading migration logs...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {githubOrg}");
        _log.LogInformation($"{GithubRepo.GetLogFriendlyName()}: {githubRepo}");

        if (githubApiUrl.HasValue())
        {
            _log.LogInformation($"{GithubApiUrl.GetLogFriendlyName()}: {githubApiUrl}");
        }

        if (githubPat.HasValue())
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        if (migrationLogFile.HasValue())
        {
            _log.LogInformation($"{MigrationLogFile.GetLogFriendlyName()}: {migrationLogFile}");
        }

        _log.RegisterSecret(githubPat);

        migrationLogFile ??= $"migration-log-{githubOrg}-{githubRepo}.log";

        if (FileExists(migrationLogFile))
        {
            if (!overwrite)
            {
                throw new OctoshiftCliException($"File {migrationLogFile} already exists!  Use --overwrite to overwrite this file.");
            }

            _log.LogWarning($"Overwriting {migrationLogFile} due to --overwrite option.");
        }

        var githubApi = _githubApiFactory.Create(githubApiUrl, githubPat);

        var result = await _retryPolicy.RetryOnResult(async () => await githubApi.GetMigrationLogUrl(githubOrg, githubRepo), string.Empty,
            "Waiting for migration log to populate...");

        if (result.Outcome == OutcomeType.Successful && result.Result is null)
        {
            throw new OctoshiftCliException($"Migration for repository {githubRepo} not found!");
        }

        if (result.Outcome == OutcomeType.Failure)
        {
            throw new OctoshiftCliException($"Migration log for repository {githubRepo} unavailable!");
        }

        var logUrl = result.Result;

        _log.LogInformation($"Downloading log for repository {githubRepo} to {migrationLogFile}...");
        await _httpDownloadService.Download(logUrl, migrationLogFile);

        _log.LogSuccess($"Downloaded {githubRepo} log to {migrationLogFile}.");
    }
}
