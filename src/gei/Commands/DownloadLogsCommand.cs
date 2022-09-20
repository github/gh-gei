using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand(
        OctoLogger log,
        ITargetGithubApiFactory targetGithubApiFactory,
        HttpDownloadService httpDownloadService,
        RetryPolicy retryPolicy) : base(log, targetGithubApiFactory, httpDownloadService, retryPolicy)
    {
        AddOptions();
        Handler = CommandHandler.Create<DownloadLogsCommandArgs>(Invoke);
    }

    protected override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        IsRequired = false,
        Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
    };

    protected override Option<string> GithubApiUrl { get; } = new("--target-api-url")
    {
        IsRequired = false,
        Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
    };

    protected override Option<string> GithubRepo { get; } = new("--target-repo")
    {
        IsRequired = true,
        Description = "Target repository to download latest log for."
    };

    protected override Option<string> GithubOrg { get; } = new("--github-target-org")
    {
        IsRequired = true,
        Description = "Target GitHub organization to download logs from."
    };

    internal async Task Invoke(DownloadLogsCommandArgs args) => await Handle(new OctoshiftCLI.Commands.DownloadLogsCommandArgs
    {
        GithubOrg = args.GithubTargetOrg,
        GithubRepo = args.TargetRepo,
        GithubApiUrl = args.TargetApiUrl,
        GithubPat = args.GithubTargetPat,
        MigrationLogFile = args.MigrationLogFile,
        Overwrite = args.Overwrite,
        Verbose = args.Verbose,
    });
}

public class DownloadLogsCommandArgs
{
    public string GithubTargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GithubTargetPat { get; set; }
    public string MigrationLogFile { get; set; }
    public bool Overwrite { get; set; }
    public bool Verbose { get; set; }
}
