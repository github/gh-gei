using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.AdoToGithub.Commands;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand() : base() => AddOptions();
}
