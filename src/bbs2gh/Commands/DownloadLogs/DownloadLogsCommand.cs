using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands.DownloadLogs;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.BbsToGithub.Commands.DownloadLogs;

public sealed class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand()
    {
        // Add backward compatibility alias for --github-api-url
        GithubApiUrl.AddAlias("--github-api-url");

        AddOptions();
    }
}
