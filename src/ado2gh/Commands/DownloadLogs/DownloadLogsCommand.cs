using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands.DownloadLogs;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.AdoToGithub.Commands.DownloadLogs;

public sealed class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() : base()
    {
        // Add backward compatibility alias for --github-api-url
        GithubApiUrl.AddAlias("--github-api-url");

        AddOptions();
    }
}
