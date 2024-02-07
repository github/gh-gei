using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.AbortMigration;

public class AbortMigrationCommandArgs : CommandArgs
{
    public string MigrationId { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string TargetApiUrl { get; set; }

    public override void Validate(OctoLogger log)
    {
        const string repoMigrationIdPrefix = "RM_";

        if (MigrationId.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--migration-id must be provided");
        }

        if (!MigrationId.StartsWith(repoMigrationIdPrefix))
        {
            throw new OctoshiftCliException($"Invalid migration ID: {MigrationId}. Only repository migration IDs starting with RM_ are supported.");
        }
    }
}
