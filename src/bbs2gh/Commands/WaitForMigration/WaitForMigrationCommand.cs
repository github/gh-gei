using OctoshiftCLI.Commands;

namespace OctoshiftCLI.BbsToGithub.Commands;

public sealed class WaitForMigrationCommand : WaitForMigrationCommandBase
{
    public WaitForMigrationCommand() => AddOptions();
}
