using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.WaitForMigration;

public class WaitForMigrationCommandArgs : CommandArgs
{
    public string MigrationId { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string TargetApiUrl { get; set; }

    public const string REPO_MIGRATION_ID_PREFIX = "RM_";
    public const string ORG_MIGRATION_ID_PREFIX = "OM_";

    public override void Validate(OctoLogger log)
    {
        if (MigrationId.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("MigrationId must be provided");
        }

        if (!MigrationId.StartsWith(REPO_MIGRATION_ID_PREFIX) && !MigrationId.StartsWith(ORG_MIGRATION_ID_PREFIX))
        {
            throw new OctoshiftCliException($"Invalid migration id: {MigrationId}");
        }
    }
}
