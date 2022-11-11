using System.CommandLine;
using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class DownloadLogsCommand : DownloadLogsCommandBase
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
        IsRequired = true,
        Description = "Target repository to download latest log for."
    };

    public override Option<string> GithubOrg { get; } = new("--github-target-org")
    {
        IsRequired = true,
        Description = "Target GitHub organization to download logs from."
    };
}
