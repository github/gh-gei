using OctoshiftCLI.Commands.WaitForMigration;

namespace OctoshiftCLI.AdoToGithub.Commands.WaitForMigration;

public sealed class WaitForMigrationCommand : WaitForMigrationCommandBase
{
    public WaitForMigrationCommand() : base() => AddOptions();
}
