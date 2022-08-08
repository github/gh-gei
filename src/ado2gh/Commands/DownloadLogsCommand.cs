using System.Runtime.CompilerServices;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.AdoToGithub.Commands;

public class DownloadLogsCommand : DownloadLogsCommandBase
{
    public DownloadLogsCommand(
        OctoLogger log,
        ITargetGithubApiFactory githubApiFactory,
        HttpDownloadService httpDownloadService,
        RetryPolicy retryPolicy) : base(log, githubApiFactory, httpDownloadService, retryPolicy)
    {
    }
}
