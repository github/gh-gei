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

    public async Task Invoke(DownloadLogsCommandArgs args) => await Handle(args);
}
