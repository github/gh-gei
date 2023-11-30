using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.AbortMigration;

public class AbortMigrationCommandArgs : CommandArgs
{
    public string MigrationId { get; set; }
    [Secret]
    public string GithubPat { get; set; }

    public const string REPO_MIGRATION_ID_PREFIX = "RM_";

    public override void Validate(OctoLogger log)
    {
        if (MigrationId.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--migration-id must be provided");
        }

        if (!MigrationId.StartsWith(REPO_MIGRATION_ID_PREFIX))
        {
            throw new OctoshiftCliException($"Invalid migration id: {MigrationId}");
        }
    }
}
