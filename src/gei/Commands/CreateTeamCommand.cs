using System.CommandLine;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class CreateTeamCommand : CreateTeamCommandBase
{
    public CreateTeamCommand() : base() => AddOptions();

    public override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        IsRequired = false,
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };
}
