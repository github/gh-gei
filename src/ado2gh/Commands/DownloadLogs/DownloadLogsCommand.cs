using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands.DownloadLogs;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.AdoToGithub.Commands.DownloadLogs;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() : base() => AddOptions();
}
