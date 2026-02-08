using System.CommandLine;
using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands.DownloadLogs;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.DownloadLogs;

public sealed class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() : base() => AddOptions();

    public override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public override Option<string> GithubApiUrl { get; } = new("--target-api-url")
    {
        Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
    };

    public override Option<string> GithubRepo { get; } = new("--target-repo")
    {
        Description = "Target repository to download latest log for."
    };

    public override Option<string> GithubOrg { get; } = new("--github-target-org")
    {
        Description = "Target GitHub organization to download logs from."
    };
}
