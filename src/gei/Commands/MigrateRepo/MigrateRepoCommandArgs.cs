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
        public string TargetUploadsUrl { get; set; }
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
        public string GitArchivePath { get; set; }
        public string MetadataArchivePath { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool QueueOnly { get; set; }
        public string TargetRepoVisibility { get; set; }
        [Secret]
        public string GithubSourcePat { get; set; }
        [Secret]
        public string GithubTargetPat { get; set; }
        public bool KeepArchive { get; set; }
        public bool UseGithubStorage { get; set; }

        public override void Validate(OctoLogger log)
        {
            DefaultSourcePat(log);
            DefaultTargetRepo(log);

            if (GitArchiveUrl.HasValue() && GitArchivePath.HasValue())
            {
                throw new OctoshiftCliException("The options --git-archive-url and --git-archive-path may not be used together");
            }

            if (MetadataArchiveUrl.HasValue() && MetadataArchivePath.HasValue())
            {
                throw new OctoshiftCliException("The options --metadata-archive-url and --metadata-archive-path may not be used together");
            }

            if (string.IsNullOrWhiteSpace(GitArchiveUrl) != string.IsNullOrWhiteSpace(MetadataArchiveUrl))
            {
                throw new OctoshiftCliException("When using archive urls, you must provide both --git-archive-url --metadata-archive-url");
            }

            if (string.IsNullOrWhiteSpace(GitArchivePath) != string.IsNullOrWhiteSpace(MetadataArchivePath))
            {
                throw new OctoshiftCliException("When using archive files, you must provide both --git-archive-path --metadata-archive-path");
            }

            if (GhesApiUrl.IsNullOrWhiteSpace())
            {
                if (AwsBucketName.HasValue() && GitArchivePath.IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("When using --aws-bucket-name, you must provide --ghes-api-url, or --git-archive-path and --metadata-archive-path");
                }

                if (UseGithubStorage && GitArchivePath.IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("When using --use-github-storage, you must provide --ghes-api-url, or --git-archive-path and --metadata-archive-path");
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

            if (AwsBucketName.HasValue() && UseGithubStorage)
            {
                throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.");
            }

            if (AzureStorageConnectionString.HasValue() && UseGithubStorage)
            {
                throw new OctoshiftCliException("The --use-github-storage flag was provided with a connection string for an Azure storage account. Archive cannot be uploaded to both locations.");
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
