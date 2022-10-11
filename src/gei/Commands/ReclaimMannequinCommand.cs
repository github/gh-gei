using System.CommandLine;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class ReclaimMannequinCommand : ReclaimMannequinCommandBase
{
    public ReclaimMannequinCommand() : base() => AddOptions();

    protected override Option<string> GithubOrg { get; } = new("--github-target-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable."
    };

    protected override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };
}
