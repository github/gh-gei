using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.AdoToGithub.Commands.WaitForMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
