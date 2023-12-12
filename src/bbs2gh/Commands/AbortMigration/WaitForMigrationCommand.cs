using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.BbsToGithub.Commands.WaitForMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
