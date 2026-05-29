using OctoshiftCLI.Commands.AbortMigration;

namespace OctoshiftCLI.GitlabToGithub.Commands.AbortMigration;

public sealed class AbortMigrationCommand : AbortMigrationCommandBase
{
    public AbortMigrationCommand() : base() => AddOptions();
}
