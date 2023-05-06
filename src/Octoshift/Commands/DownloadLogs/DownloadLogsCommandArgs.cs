
namespace OctoshiftCLI.Commands.DownloadLogs;

public class DownloadLogsCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string GithubApiUrl { get; set; }
    public string GithubPat { get; set; }
    public string MigrationLogFile { get; set; }
    public bool Overwrite { get; set; }
}
