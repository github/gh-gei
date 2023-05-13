using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public bool Wait { get; set; }
        public bool QueueOnly { get; set; }
        public string TargetRepoVisibility { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        [Secret]
        public string GithubPat { get; set; }

        public override void Validate(OctoLogger log)
        {
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
