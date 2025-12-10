using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Factories;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo
{
    public class MigrateRepoCommand : CommandBase<MigrateRepoCommandArgs, MigrateRepoCommandHandler>
    {
        public MigrateRepoCommand() : base(
            name: "migrate-repo",
            description: "Invokes the GitHub APIs to migrate the repo and all repo data.")
        {
            AddOption(GithubSourceOrg);
            AddOption(SourceRepo);
            AddOption(GithubTargetOrg);
            AddOption(TargetRepo);
            AddOption(TargetApiUrl);
            AddOption(TargetUploadsUrl);
            AddOption(GhesApiUrl);
            AddOption(AzureStorageConnectionString);
            AddOption(AwsBucketName);
            AddOption(AwsAccessKey);
            AddOption(AwsSecretKey);
            AddOption(AwsSessionToken);
            AddOption(AwsRegion);
            AddOption(NoSslVerify);
            AddOption(GitArchiveUrl);
            AddOption(MetadataArchiveUrl);
            AddOption(GitArchivePath);
            AddOption(MetadataArchivePath);
            AddOption(SkipReleases);
            AddOption(LockSourceRepo);
            AddOption(QueueOnly);
            AddOption(TargetRepoVisibility.FromAmong("public", "private", "internal"));
            AddOption(GithubSourcePat);
            AddOption(GithubTargetPat);
            AddOption(Verbose);
            AddOption(KeepArchive);
            AddOption(UseGithubStorage);
        }

        public Option<string> GithubSourceOrg { get; } = new("--github-source-org")
        {
            Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set.",
            IsRequired = true,
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
        public Option<string> TargetUploadsUrl { get; } = new(
            name: "--target-uploads-url",
            description: "The URL of the target uploads API, if not migrating to github.com. Defaults to https://uploads.github.com")
        { IsHidden = true };

        // GHES migration path
        public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
        {
            Description = "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
        };
        public Option<string> AzureStorageConnectionString { get; } = new("--azure-storage-connection-string")
        {
            Description = "Required if migrating from GHES (Not required if migrating from GitHub Enterprise Server 3.8.0 or later). The connection string for the Azure storage account, used to upload data archives pre-migration. For example: \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\""
        };
        public Option<string> AwsBucketName { get; } = new("--aws-bucket-name")
        {
            Description = "If using AWS, the name of the S3 bucket to upload the data archives to. Not required if migrating from GitHub Enterprise Server 3.8.0 or later."
        };
        public Option<string> AwsAccessKey { get; } = new("--aws-access-key")
        {
            Description = "If uploading to S3, the AWS access key. If not provided, it will be read from AWS_ACCESS_KEY_ID environment variable. Not required if migrating from GitHub Enterprise Server 3.8.0 or later."
        };
        public Option<string> AwsSecretKey { get; } = new("--aws-secret-key")
        {
            Description = "If uploading to S3, the AWS secret key. If not provided, it will be read from AWS_SECRET_ACCESS_KEY environment variable. Not required if migrating from GitHub Enterprise Server 3.8.0 or later."
        };
        public Option<string> AwsSessionToken { get; } = new("--aws-session-token")
        {
            Description = "If using AWS, the AWS session token. If not provided, it will be read from AWS_SESSION_TOKEN environment variable."
        };
        public Option<string> AwsRegion { get; } = new("--aws-region")
        {
            Description = "If using AWS, the AWS region. If not provided, it will be read from AWS_REGION environment variable. " +
                          "Required if using AWS."
        };
        public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify")
        {
            Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
        };
        public Option<bool> UseGithubStorage { get; } = new("--use-github-storage")
        {
            IsHidden = true,
            Description = "Enables multipart uploads to a GitHub owned storage for use during migration. " +
                          "Configure chunk size with the GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES environment variable (default: 100 MiB, minimum: 5 MiB).",
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
        public Option<string> GitArchivePath { get; } = new("--git-archive-path")
        {
            IsHidden = true,
            Description = "Used to migrate an archive that is on disk, must be used with --metadata-archive-path"
        };
        public Option<string> MetadataArchivePath { get; } = new("--metadata-archive-path")
        {
            IsHidden = true,
            Description = "Used to migrate an archive that is on disk, must be used with --git-archive-path"
        };
        public Option<bool> SkipReleases { get; } = new("--skip-releases")
        {
            Description = "Skip releases when migrating."
        };
        public Option<bool> LockSourceRepo { get; } = new("--lock-source-repo")
        {
            Description = "Lock source repo when migrating."
        };
        public Option<bool> QueueOnly { get; } = new("--queue-only")
        {
            Description = "Only queues the migration, does not wait for it to finish. Use the wait-for-migration command to subsequently wait for it to finish and view the status."
        };
        public Option<string> TargetRepoVisibility { get; } = new("--target-repo-visibility")
        {
            Description = "The visibility of the target repo. Defaults to private. Valid values are public, private, or internal."
        };
        public Option<string> GithubSourcePat { get; } = new("--github-source-pat");
        public Option<string> GithubTargetPat { get; } = new("--github-target-pat");
        public Option<bool> Verbose { get; } = new("--verbose");

        public Option<bool> KeepArchive { get; } = new("--keep-archive")
        {
            Description = "Keeps the archive on this machine after uploading to the blob storage account. Only applicable for migrations from GitHub Enterprise Server versions before 3.8.0 or when used with --use-github-storage."
        };

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
            var fileSystemProvider = sp.GetRequiredService<FileSystemProvider>();
            var ghesVersionCheckerFactory = sp.GetRequiredService<GhesVersionCheckerFactory>();
            var retryPolicy = sp.GetRequiredService<RetryPolicy>();

            var targetGithubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
            var targetGithubApi = targetGithubApiFactory.Create(args.TargetApiUrl, args.TargetUploadsUrl, args.GithubTargetPat);

            GithubApi ghesApi = null;
            AzureApi azureApi = null;
            AwsApi awsApi = null;
            HttpDownloadService httpDownloadService = null;

            if (args.GhesApiUrl.HasValue() || (args.GitArchivePath.HasValue() && args.MetadataArchivePath.HasValue()))
            {
                var sourceGithubApiFactory = sp.GetRequiredService<ISourceGithubApiFactory>();
                var awsApiFactory = sp.GetRequiredService<AwsApiFactory>();
                var azureApiFactory = sp.GetRequiredService<IAzureApiFactory>();
                var httpDownloadServiceFactory = sp.GetRequiredService<HttpDownloadServiceFactory>();
                ghesApi = args.NoSslVerify ? sourceGithubApiFactory.CreateClientNoSsl(args.GhesApiUrl, args.GithubSourcePat) : sourceGithubApiFactory.Create(args.GhesApiUrl, args.GithubSourcePat);
                httpDownloadService = args.NoSslVerify ? httpDownloadServiceFactory.CreateClientNoSsl() : httpDownloadServiceFactory.CreateDefault();

                if (args.AzureStorageConnectionString.HasValue() || environmentVariableProvider.AzureStorageConnectionString(false).HasValue())
                {
                    azureApi = args.NoSslVerify ? azureApiFactory.CreateClientNoSsl(args.AzureStorageConnectionString) : azureApiFactory.Create(args.AzureStorageConnectionString);
                }

                if (args.AwsBucketName.HasValue())
                {
                    awsApi = awsApiFactory.Create(args.AwsRegion, args.AwsAccessKey, args.AwsSecretKey, args.AwsSessionToken);
                }
            }

            var ghesVersionChecker = ghesVersionCheckerFactory.Create(ghesApi);
            var warningsCountLogger = sp.GetRequiredService<WarningsCountLogger>();

            return new MigrateRepoCommandHandler(
                log,
                ghesApi,
                targetGithubApi,
                environmentVariableProvider,
                azureApi,
                awsApi,
                httpDownloadService,
                fileSystemProvider,
                ghesVersionChecker,
                retryPolicy,
                warningsCountLogger
            );
        }
    }
}
