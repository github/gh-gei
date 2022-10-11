using System.CommandLine;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public sealed class CreateTeamCommand : CreateTeamCommandBase
{
    public CreateTeamCommand() : base() => AddOptions();

    protected override Option<string> GithubPat { get; } = new("--github-target-pat") { IsRequired = false };
}
