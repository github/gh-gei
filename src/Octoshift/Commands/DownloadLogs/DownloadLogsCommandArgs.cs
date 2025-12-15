using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.DownloadLogs;

public class DownloadLogsCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string MigrationId { get; set; }
    public string GithubApiUrl { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string MigrationLogFile { get; set; }
    public bool Overwrite { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GithubOrg.IsUrl())
        {
            throw new OctoshiftCliException("The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        if (GithubRepo.IsUrl())
        {
            throw new OctoshiftCliException("The --github-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
        }
    }
}
