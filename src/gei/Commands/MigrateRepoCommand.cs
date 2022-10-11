using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : Command
    {
        public MigrateRepoCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider, IAzureApiFactory azureApiFactory, AwsApiFactory awsApiFactory) : base(
            name: "migrate-repo",
            description: "Invokes the GitHub APIs to migrate the repo and all repo data.")
        {
            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
            };
            var adoServerUrl = new Option<string>("--ado-server-url")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
            };
            var adoSourceOrg = new Option<string>("--ado-source-org")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Uses ADO_PAT env variable or --ado-pat option."
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false,
                IsHidden = true
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-target-pat option."
            };
            var targetRepo = new Option<string>("--target-repo")
            {
                IsRequired = false,
                Description = "Defaults to the name of source-repo"
            };
            var targetApiUrl = new Option<string>("--target-api-url")
            {
                IsRequired = false,
                Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
            };

            // GHES migration path
            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The connection string for the Azure storage account, used to upload data archives pre-migration. For example: \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\""
            };

            var awsBucketName = new Option<string>("--aws-bucket-name")
            {
                IsRequired = false,
                Description = "If using AWS, the name of the S3 bucket to upload the BBS archive to."
            };
            var awsAccessKey = new Option<string>("--aws-access-key")
            {
                IsRequired = false,
                Description = "If uploading to S3, the AWS access key. If not provided, it will be read from AWS_ACCESS_KEY environment variable."
            };
            var awsSecretKey = new Option<string>("--aws-secret-key")
            {
                IsRequired = false,
                Description = "If uploading to S3, the AWS secret key. If not provided, it will be read from AWS_SECRET_KEY environment variable."
            };

            var noSslVerify = new Option<bool>("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
            };

            // Pre-uploaded archive urls, hidden by default
            var gitArchiveUrl = new Option<string>("--git-archive-url")
            {
                IsHidden = true,
                IsRequired = false,
                Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated git archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --metadata-archive-url"
            };
            var metadataArchiveUrl = new Option<string>("--metadata-archive-url")
            {
                IsHidden = true,
                IsRequired = false,
                Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated metadata archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --git-archive-url"
            };
            var skipReleases = new Option<bool>("--skip-releases")
            {
                IsRequired = false,
                Description = "Skip releases when migrating."
            };
            var lockSourceRepo = new Option<bool>("--lock-source-repo")
            {
                IsRequired = false,
                Description = "Lock source repo when migrating."
            };
            var wait = new Option<bool>("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the repo migration to finish."
            };
            var githubSourcePat = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false,
                IsHidden = true
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(adoServerUrl);
            AddOption(adoSourceOrg);
            AddOption(adoTeamProject);
            AddOption(sourceRepo);
            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(targetApiUrl);

            AddOption(ghesApiUrl);
            AddOption(azureStorageConnectionString);
            AddOption(awsBucketName);
            AddOption(awsAccessKey);
            AddOption(awsSecretKey);
            AddOption(noSslVerify);

            AddOption(gitArchiveUrl);
            AddOption(metadataArchiveUrl);

            AddOption(skipReleases);
            AddOption(lockSourceRepo);

            AddOption(wait);
            AddOption(githubSourcePat);
            AddOption(githubTargetPat);
            AddOption(adoPat);
            AddOption(verbose);

            var handler = new MigrateRepoCommandHandler(log, sourceGithubApiFactory, targetGithubApiFactory, environmentVariableProvider, azureApiFactory, awsApiFactory, new FileDownloader(new HttpClient(), log));
            Handler = CommandHandler.Create<MigrateRepoCommandArgs>(handler.Invoke);
        }
    }

    public class MigrateRepoCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string AdoServerUrl { get; set; }
        public string AdoSourceOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string SourceRepo { get; set; }
        public string GithubTargetOrg { get; set; }
        public string TargetRepo { get; set; }
        public string TargetApiUrl { get; set; }
        public string GhesApiUrl { get; set; }
        public string AzureStorageConnectionString { get; set; }
        public string AwsBucketName { get; set; }
        public string AwsAccessKey { get; set; }
        public string AwsSecretKey { get; set; }
        public bool NoSslVerify { get; set; }
        public string GitArchiveUrl { get; set; }
        public string MetadataArchiveUrl { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool Wait { get; set; }
        public bool Verbose { get; set; }
        public string GithubSourcePat { get; set; }
        public string GithubTargetPat { get; set; }
        public string AdoPat { get; set; }
    }
}
