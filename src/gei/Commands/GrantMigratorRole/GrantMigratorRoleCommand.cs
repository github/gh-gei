using System.CommandLine;
using OctoshiftCLI.Commands.GrantMigratorRole;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.GrantMigratorRole;

public sealed class GrantMigratorRoleCommand : GrantMigratorRoleCommandBase
{
    public GrantMigratorRoleCommand() : base() => AddOptions();

    public override Option<string> GithubPat { get; } = new("--github-target-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };
}
