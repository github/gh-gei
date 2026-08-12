using System;
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
        public string GithubSourceApiUrl { get; set; }
        public string AwsBucketName { get; set; }
        public string AwsRegion { get; set; }
        public bool NoSslVerify { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool Sequential { get; set; }
        [Secret]
        public string GithubSourcePat { get; set; }
        public bool KeepArchive { get; set; }
        public string TargetApiUrl { get; set; }
        public string TargetUploadsUrl { get; set; }
        public bool UseGithubStorage { get; set; }

        public string GetSourceApiUrl() => GithubSourceApiUrl.HasValue() ? GithubSourceApiUrl : GhesApiUrl;

        public override void Validate(OctoLogger log)
        {
            if (GithubSourceOrg.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
            }

            if (GithubTargetOrg.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
            }

            if (GithubSourceApiUrl.IsNullOrWhiteSpace())
            {
                GithubSourceApiUrl = System.Environment.GetEnvironmentVariable("GH_SOURCE_API_URL");
            }

            if (GhesApiUrl.HasValue() && GithubSourceApiUrl.HasValue())
            {
                throw new OctoshiftCliException("Only one of --github-source-api-url or --ghes-api-url may be specified.");
            }

            if (GithubSourceApiUrl.HasValue() && !GithubSourceApiUrl.IsProximaApiUrl())
            {
                throw new OctoshiftCliException("--github-source-api-url must be a valid GitHub Enterprise Cloud with data residency API URL (e.g. https://api.tenant.ghe.com).");
            }

            var sourceApiUrl = GetSourceApiUrl();

            if (AwsBucketName.HasValue())
            {
                if (sourceApiUrl.IsNullOrWhiteSpace())
                {
                    throw new OctoshiftCliException("--ghes-api-url or --github-source-api-url must be specified when --aws-bucket-name is specified.");
                }

                if (UseGithubStorage)
                {
                    throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.");
                }
            }

            if (NoSslVerify && GhesApiUrl.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
            }

            if (sourceApiUrl.IsNullOrWhiteSpace() && UseGithubStorage)
            {
                throw new OctoshiftCliException("--ghes-api-url or --github-source-api-url must be specified when --use-github-storage is specified.");
            }

            if (sourceApiUrl.HasValue())
            {
                var result = Uri.TryCreate(sourceApiUrl, UriKind.Absolute, out var uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                if (!result)
                {
                    throw new OctoshiftCliException(GithubSourceApiUrl.HasValue()
                        ? "--github-source-api-url is invalid. Please check URL before trying again."
                        : "--ghes-api-url is invalid. Please check URL before trying again.");
                }
            }
        }
    }
}
