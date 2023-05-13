using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateOrg
{
    public class MigrateOrgCommandArgs : CommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string GithubTargetOrg { get; set; }
        public string GithubTargetEnterprise { get; set; }
        public bool Wait { get; set; }
        public bool QueueOnly { get; set; }
        [Secret]
        public string GithubSourcePat { get; set; }
        [Secret]
        public string GithubTargetPat { get; set; }

        public override void Validate(OctoLogger log)
        {
            if (GithubTargetPat.HasValue())
            {
                if (GithubSourcePat.IsNullOrWhiteSpace())
                {
                    GithubSourcePat = GithubTargetPat;
                    log?.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
                }
            }

            if (Wait)
            {
                log?.LogWarning("--wait flag is obsolete and will be removed in a future version. The default behavior is now to wait.");
            }

            if (Wait && QueueOnly)
            {
                throw new OctoshiftCliException("You can't specify both --wait and --queue-only at the same time.");
            }

            if (!Wait && !QueueOnly)
            {
                log?.LogWarning("The default behavior has changed from only queueing the migration, to waiting for the migration to finish. If you ran this as part of a script to run multiple migrations in parallel, consider using the new --queue-only option to preserve the previous default behavior. This warning will be removed in a future version.");
            }
        }
    }
}
