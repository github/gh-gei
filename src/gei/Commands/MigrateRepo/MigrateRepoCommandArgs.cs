using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgs : CommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string SourceRepo { get; set; }
        public string GithubTargetOrg { get; set; }
        public string TargetRepo { get; set; }
        public string TargetApiUrl { get; set; }
        public string GhesApiUrl { get; set; }
        [Secret]
        public string AzureStorageConnectionString { get; set; }
        public string AwsBucketName { get; set; }
        [Secret]
        public string AwsAccessKey { get; set; }
        [Secret]
        public string AwsSecretKey { get; set; }
        [Secret]
        public string AwsSessionToken { get; set; }
        public string AwsRegion { get; set; }
        public bool NoSslVerify { get; set; }
        public string GitArchiveUrl { get; set; }
        public string MetadataArchiveUrl { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool Wait { get; set; }
        public bool QueueOnly { get; set; }
        public string TargetRepoVisibility { get; set; }
        [Secret]
        public string GithubSourcePat { get; set; }
        [Secret]
        public string GithubTargetPat { get; set; }
        [Secret]
        public bool KeepArchive { get; set; }

        public override void Validate(OctoLogger log)
        {
            DefaultSourcePat(log);
            DefaultTargetRepo(log);

            if (string.IsNullOrWhiteSpace(GitArchiveUrl) != string.IsNullOrWhiteSpace(MetadataArchiveUrl))
            {
                throw new OctoshiftCliException("When using archive urls, you must provide both --git-archive-url --metadata-archive-url");
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

            if (GhesApiUrl.IsNullOrWhiteSpace())
            {
                if (AwsBucketName.HasValue())
                {
                    throw new OctoshiftCliException("--ghes-api-url must be specified when --aws-bucket-name is specified.");
                }

                if (NoSslVerify)
                {
                    throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
                }

                if (KeepArchive)
                {
                    throw new OctoshiftCliException("--ghes-api-url must be specified when --keep-archive is specified.");
                }
            }
        }

        private void DefaultTargetRepo(OctoLogger log)
        {
            if (TargetRepo.IsNullOrWhiteSpace())
            {
                log?.LogInformation($"Target repo name not provided, defaulting to same as source repo ({SourceRepo})");
                TargetRepo = SourceRepo;
            }
        }

        private void DefaultSourcePat(OctoLogger log)
        {
            if (GithubTargetPat.HasValue() && GithubSourcePat.IsNullOrWhiteSpace())
            {
                GithubSourcePat = GithubTargetPat;
                log?.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
            }
        }
    }
}
