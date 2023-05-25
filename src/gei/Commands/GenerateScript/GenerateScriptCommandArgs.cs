using System.IO;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommandArgs : CommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string GithubTargetOrg { get; set; }
        public FileInfo Output { get; set; }
        public string GhesApiUrl { get; set; }
        public string AwsBucketName { get; set; }
        public string AwsRegion { get; set; }
        public bool NoSslVerify { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool Sequential { get; set; }
        [Secret]
        public string GithubSourcePat { get; set; }
        [Secret]
        public bool KeepArchive { get; set; }

        public override void Validate(OctoLogger log)
        {
            if (AwsBucketName.HasValue() && GhesApiUrl.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("--ghes-api-url must be specified when --aws-bucket-name is specified.");
            }

            if (NoSslVerify && GhesApiUrl.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
            }
        }
    }
}
