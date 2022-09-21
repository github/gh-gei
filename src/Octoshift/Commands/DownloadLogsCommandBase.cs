using System;
using System.CommandLine;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Handlers;
using Polly;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands;

public class DownloadLogsCommandBase : Command
{
    protected DownloadLogsCommandBaseHandler BaseHandler { get; init; }

    public DownloadLogsCommandBase(
        OctoLogger log,
        ITargetGithubApiFactory githubApiFactory,
        HttpDownloadService httpDownloadService,
        RetryPolicy retryPolicy) : base(name: "download-logs", description: "Downloads migration logs for migrations.")
    {
        BaseHandler = new DownloadLogsCommandBaseHandler(log, githubApiFactory, httpDownloadService, retryPolicy);
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
