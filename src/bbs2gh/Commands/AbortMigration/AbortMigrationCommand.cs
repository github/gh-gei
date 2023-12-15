using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.BbsToGithub.Commands.AbortMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
