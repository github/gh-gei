using System.CommandLine;
using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() : base() => AddOptions();

    protected override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
    };

    protected override Option<string> GithubApiUrl { get; } = new("--target-api-url")
    {
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
}
