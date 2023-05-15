using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands.DownloadLogs;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.BbsToGithub.Commands.DownloadLogs;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() => AddOptions();
}
