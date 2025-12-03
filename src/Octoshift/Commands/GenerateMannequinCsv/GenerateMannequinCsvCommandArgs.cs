using System.IO;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.GenerateMannequinCsv;

public class GenerateMannequinCsvCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public FileInfo Output { get; set; }
    public bool IncludeReclaimed { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string TargetApiUrl { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GithubOrg.IsUrl())
        {
            throw new OctoshiftCliException("The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }
    }
}
