using System;
using System.CommandLine;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.Commands;

public class DownloadLogsCommandBase : CommandBase<DownloadLogsCommandArgs, DownloadLogsCommandHandler>
{
    public DownloadLogsCommandBase() : base(name: "download-logs", description: "Downloads migration logs for migrations.")
    {
    }

    public virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "GitHub organization to download logs from."
    };

    public virtual Option<string> GithubRepo { get; } = new("--github-repo")
    {
        IsRequired = true,
        Description = "Target repository to download latest log for."
    };

    public virtual Option<string> GithubApiUrl { get; } = new("--github-api-url")
    {
        Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
    };

    public virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public virtual Option<string> MigrationLogFile { get; } = new("--migration-log-file")
    {
        Description = "Local file to write migration log to (default: migration-log-ORG-REPO.log)."
    };

    public virtual Option<bool> Overwrite { get; } = new("--overwrite")
    {
        Description = "Overwrite migration log file if it exists."
    };

    public virtual Option<bool> Verbose { get; } = new("--verbose")
    {
        Description = "Display more information to the console."
    };

    public override DownloadLogsCommandHandler BuildHandler(DownloadLogsCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var githubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
        var githubApi = githubApiFactory.Create(args.GithubApiUrl, args.GithubPat);
        var httpDownloadService = sp.GetRequiredService<HttpDownloadService>();
        var retryPolicy = sp.GetRequiredService<RetryPolicy>();

        return new DownloadLogsCommandHandler(log, githubApi, httpDownloadService, retryPolicy);
    }

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
