using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.WaitForMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
