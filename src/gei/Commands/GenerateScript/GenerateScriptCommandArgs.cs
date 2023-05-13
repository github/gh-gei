using System.IO;
using System.Linq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommandArgs : CommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string AdoServerUrl { get; set; }
        public string AdoSourceOrg { get; set; }
        public string AdoTeamProject { get; set; }
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
        public string AdoPat { get; set; }
        public bool KeepArchive { get; set; }

        public override void Validate(OctoLogger log)
        {
            var hasAdoSpecificArg = new[] { AdoPat, AdoServerUrl, AdoSourceOrg, AdoTeamProject }.Any(arg => arg.HasValue());

            if (hasAdoSpecificArg)
            {
                log?.LogWarning("ADO migration feature will be removed from `gh gei` in near future, please consider switching to `gh ado2gh` for ADO migrations instead.");
            }

            if (GithubSourceOrg.IsNullOrWhiteSpace() && AdoSourceOrg.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
            }

            if (AdoServerUrl.HasValue() && !AdoSourceOrg.HasValue())
            {
                throw new OctoshiftCliException("Must specify --ado-source-org with the collection name when using --ado-server-url");
            }

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
