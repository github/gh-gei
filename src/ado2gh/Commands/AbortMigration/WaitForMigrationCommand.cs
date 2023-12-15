using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.AdoToGithub.Commands.AbortMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
