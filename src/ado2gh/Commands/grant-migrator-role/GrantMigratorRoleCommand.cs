using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class GrantMigratorRoleCommand : GrantMigratorRoleCommandBase
{
    public GrantMigratorRoleCommand() : base() => AddOptions();
}
