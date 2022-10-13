using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class RevokeMigratorRoleCommand : RevokeMigratorRoleCommandBase
{
    public RevokeMigratorRoleCommand() : base() => AddOptions();
}
