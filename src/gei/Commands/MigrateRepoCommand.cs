using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommand : CommandBase<MigrateRepoCommandArgs, MigrateRepoCommandHandler>
    {
        public MigrateRepoCommand() : base(
            name: "migrate-repo",
            description: "Invokes the GitHub APIs to migrate the repo and all repo data.")
        {
            AddOption(GithubSourceOrg);
            AddOption(AdoServerUrl);
            AddOption(AdoSourceOrg);
            AddOption(AdoTeamProject);
            AddOption(SourceRepo);
            AddOption(GithubTargetOrg);
            AddOption(TargetRepo);
            AddOption(TargetApiUrl);

            AddOption(GhesApiUrl);
            AddOption(AzureStorageConnectionString);
            AddOption(AwsBucketName);
            AddOption(AwsAccessKey);
            AddOption(AwsSecretKey);
            AddOption(NoSslVerify);

            AddOption(GitArchiveUrl);
            AddOption(MetadataArchiveUrl);

            AddOption(SkipReleases);
            AddOption(LockSourceRepo);

            AddOption(Wait);
            AddOption(GithubSourcePat);
            AddOption(GithubTargetPat);
            AddOption(AdoPat);
            AddOption(Verbose);
        }

        public Option<string> GithubSourceOrg { get; } = new("--github-source-org")
        {
            Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
        };
        public Option<string> AdoServerUrl { get; } = new("--ado-server-url")
        {
            IsHidden = true,
            Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
        };
        public Option<string> AdoSourceOrg { get; } = new("--ado-source-org")
        {
            IsHidden = true,
            Description = "Uses ADO_PAT env variable or --ado-pat option."
        };
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsHidden = true
        };
        public Option<string> SourceRepo { get; } = new("--source-repo")
        {
            IsRequired = true
        };
        public Option<string> GithubTargetOrg { get; } = new("--github-target-org")
        {
            IsRequired = true,
            Description = "Uses GH_PAT env variable or --github-target-pat option."
        };
        public Option<string> TargetRepo { get; } = new("--target-repo")
        {
            Description = "Defaults to the name of source-repo"
        };
        public Option<string> TargetApiUrl { get; } = new("--target-api-url")
        {
            Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };

        // GHES migration path
        public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
        {
            Description = "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
        };
        public Option<string> AzureStorageConnectionString { get; } = new("--azure-storage-connection-string")
        {
            Description = "Required if migrating from GHES. The connection string for the Azure storage account, used to upload data archives pre-migration. For example: \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\""
        };
        public Option<string> AwsBucketName { get; } = new("--aws-bucket-name")
        {
            Description = "If using AWS, the name of the S3 bucket to upload the BBS archive to."
        };
        public Option<string> AwsAccessKey { get; } = new("--aws-access-key")
        {
            Description = "If uploading to S3, the AWS access key. If not provided, it will be read from AWS_ACCESS_KEY environment variable."
        };
        public Option<string> AwsSecretKey { get; } = new("--aws-secret-key")
        {
            Description = "If uploading to S3, the AWS secret key. If not provided, it will be read from AWS_SECRET_KEY environment variable."
        };
        public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify")
        {
            Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
        };

        // Pre-uploaded archive urls, hidden by default
        public Option<string> GitArchiveUrl { get; } = new("--git-archive-url")
        {
            IsHidden = true,
            Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated git archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --metadata-archive-url"
        };
        public Option<string> MetadataArchiveUrl { get; } = new("--metadata-archive-url")
        {
            IsHidden = true,
            Description = "An authenticated SAS URL to an Azure Blob Storage container with a pre-generated metadata archive. Only used when an archive has been generated and uploaded prior to running a migration (not common). Must be passed in when also using --git-archive-url"
        };
        public Option<bool> SkipReleases { get; } = new("--skip-releases")
        {
            Description = "Skip releases when migrating."
        };
        public Option<bool> LockSourceRepo { get; } = new("--lock-source-repo")
        {
            Description = "Lock source repo when migrating."
        };
        public Option<bool> Wait { get; } = new("--wait")
        {
            Description = "Synchronously waits for the repo migration to finish."
        };
        public Option<string> GithubSourcePat { get; } = new("--github-source-pat");
        public Option<string> GithubTargetPat { get; } = new("--github-target-pat");
        public Option<string> AdoPat { get; } = new("--ado-pat")
        {
            IsHidden = true
        };
        public Option<bool> Verbose { get; } = new("--verbose");

        public override MigrateRepoCommandHandler BuildHandler(MigrateRepoCommandArgs args, IServiceProvider sp)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (sp is null)
            {
                throw new ArgumentNullException(nameof(sp));
            }

            var log = sp.GetRequiredService<OctoLogger>();
            var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
            var httpDownloadService = sp.GetRequiredService<HttpDownloadService>();

            var targetGithubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
            var targetGithubApi = targetGithubApiFactory.Create(args.TargetApiUrl, args.GithubTargetPat);

            GithubApi ghesApi = null;
            AzureApi azureApi = null;
            AwsApi awsApi = null;

            if (args.GhesApiUrl.HasValue())
            {
                var sourceGithubApiFactory = sp.GetRequiredService<ISourceGithubApiFactory>();
                var awsApiFactory = sp.GetRequiredService<AwsApiFactory>();
                var azureApiFactory = sp.GetRequiredService<IAzureApiFactory>();

                ghesApi = args.NoSslVerify ? sourceGithubApiFactory.CreateClientNoSsl(args.GhesApiUrl, args.GithubSourcePat) : sourceGithubApiFactory.Create(args.GhesApiUrl, args.GithubSourcePat);

                if (args.AzureStorageConnectionString.HasValue() || environmentVariableProvider.AzureStorageConnectionString().HasValue())
                {
                    azureApi = args.NoSslVerify ? azureApiFactory.CreateClientNoSsl(args.AzureStorageConnectionString) : azureApiFactory.Create(args.AzureStorageConnectionString);
                }

                if (args.AwsBucketName.HasValue())
                {
                    awsApi = awsApiFactory.Create(args.AwsAccessKey, args.AwsSecretKey);
                }
            }

            return new MigrateRepoCommandHandler(log, ghesApi, targetGithubApi, environmentVariableProvider, azureApi, awsApi, httpDownloadService);
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
