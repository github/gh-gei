using System.CommandLine;
using System.CommandLine.Invocation;
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
        Handler = CommandHandler.Create<string, string, string, string, string, bool, bool>(Invoke);
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

    public async Task Invoke(
        string githubTargetOrg,
        string targetRepo,
        string targetApiUrl = null,
        string githubTargetPat = null,
        string migrationLogFile = null,
        bool overwrite = false,
        bool verbose = false) => await Handle(githubTargetOrg, targetRepo, targetApiUrl, githubTargetPat, migrationLogFile, overwrite, verbose);
}
