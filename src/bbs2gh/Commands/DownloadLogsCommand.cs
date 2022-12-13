using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.BbsToGithub.Commands;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() => AddOptions();
}
