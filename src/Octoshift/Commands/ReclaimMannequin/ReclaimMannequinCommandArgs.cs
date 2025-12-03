using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public string Csv { get; set; }
    public string MannequinUser { get; set; }
    public string MannequinId { get; set; }
    public string TargetUser { get; set; }
    public bool Force { get; set; }
    public bool NoPrompt { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public bool SkipInvitation { get; set; }
    public string TargetApiUrl { get; set; }
    public override void Validate(OctoLogger log)
    {
        if (GithubOrg.IsUrl())
        {
            throw new OctoshiftCliException("The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        if (string.IsNullOrEmpty(Csv) && (string.IsNullOrEmpty(MannequinUser) || string.IsNullOrEmpty(TargetUser)))
        {
            throw new OctoshiftCliException($"Either --csv or --mannequin-user and --target-user must be specified");
        }
    }
}
