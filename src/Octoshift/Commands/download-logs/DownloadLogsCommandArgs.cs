namespace OctoshiftCLI.Commands;

public class DownloadLogsCommandArgs
{
    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string GithubApiUrl { get; set; }
    public string GithubPat { get; set; }
    public string MigrationLogFile { get; set; }
    public bool Overwrite { get; set; }
    public bool Verbose { get; set; }
}
