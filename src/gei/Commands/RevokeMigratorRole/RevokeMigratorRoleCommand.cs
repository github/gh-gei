using System.CommandLine;
using OctoshiftCLI.Commands.RevokeMigratorRole;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.RevokeMigratorRole;

public sealed class RevokeMigratorRoleCommand : RevokeMigratorRoleCommandBase
{
    public RevokeMigratorRoleCommand() : base() => AddOptions();

    public override Option<string> GithubPat { get; } = new("--github-target-pat");
}
