using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.AbortMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
