using System;
using System.CommandLine;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommand : CommandBase<GenerateScriptCommandArgs, GenerateScriptCommandHandler>
    {
        public GenerateScriptCommand() : base(
                name: "generate-script",
                description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.")
        {
            AddOption(GithubSourceOrg);
            AddOption(GithubTargetOrg);

            AddOption(TargetApiUrl);
            AddOption(TargetUploadsUrl);
            AddOption(GhesApiUrl);
            AddOption(AwsBucketName);
            AddOption(AwsRegion);
            AddOption(NoSslVerify);
            AddOption(DownloadMigrationLogs);

            AddOption(SkipReleases);
            AddOption(LockSourceRepo);

            AddOption(Output);
            AddOption(Sequential);
            AddOption(GithubSourcePat);
            AddOption(Verbose);
            AddOption(KeepArchive);
            AddOption(UseGithubStorage);
        }
        public Option<string> GithubSourceOrg { get; } = new("--github-source-org")
        {
            Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT if not set.",
            IsRequired = true,
        };
        public Option<string> GithubTargetOrg { get; } = new("--github-target-org")
        {
            IsRequired = true
        };

        // GHES migration path
        public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
        {
            Description = "Required if migrating from GHES. The api endpoint for the hostname of your GHES instance. For example: http(s)://myghes.com/api/v3"
        };
        public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify")
        {
            Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
        };
        public Option<bool> SkipReleases { get; } = new("--skip-releases")
        {
            Description = "Skip releases when migrating."
        };
        public Option<bool> LockSourceRepo { get; } = new("--lock-source-repo")
        {
            Description = "Lock the source repository when migrating."
        };

        public Option<bool> DownloadMigrationLogs { get; } = new("--download-migration-logs")
        {
            Description = "Downloads the migration log for each repository migration."
        };

        public Option<FileInfo> Output { get; } = new("--output", () => new FileInfo("./migrate.ps1"));

        public Option<bool> Sequential { get; } = new("--sequential")
        {
            Description = "Waits for each migration to finish before moving on to the next one."
        };
        public Option<string> GithubSourcePat { get; } = new("--github-source-pat");

        public Option<string> AwsBucketName { get; } = new("--aws-bucket-name")
        {
            Description = "If using AWS, the name of the S3 bucket to upload the BBS archive to."
        };

        public Option<string> AwsRegion { get; } = new("--aws-region")
        {
            Description = "If using AWS, the AWS region. If not provided, it will be read from AWS_REGION environment variable. " +
                          "Required if using AWS."
        };

        public Option<bool> Verbose { get; } = new("--verbose");

        public Option<bool> KeepArchive { get; } = new("--keep-archive")
        {
            Description = "Keeps the archive on this machine after uploading to the blob storage account. Only applicable for migrations from GitHub Enterprise Server versions before 3.8.0."
        };
        public Option<string> TargetApiUrl { get; } = new("--target-api-url")
        {
            Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };
        public Option<string> TargetUploadsUrl { get; } = new(
            name: "--target-uploads-url",
            description: "The URL of the target uploads API, if not migrating to github.com. Defaults to https://uploads.github.com")
        { IsHidden = true };
        public Option<bool> UseGithubStorage { get; } = new("--use-github-storage")
        {
            IsHidden = true,
            Description = "Enables multipart uploads to a GitHub owned storage for use during migration",
        };

        public override GenerateScriptCommandHandler BuildHandler(GenerateScriptCommandArgs args, IServiceProvider sp)
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
            var versionProvider = sp.GetRequiredService<IVersionProvider>();
            var ghesVersionCheckerFactory = sp.GetRequiredService<GhesVersionCheckerFactory>();

            var sourceGithubApiFactory = sp.GetRequiredService<ISourceGithubApiFactory>();

            var sourceGithubApi = args.GhesApiUrl.HasValue() && args.NoSslVerify ?
                sourceGithubApiFactory.CreateClientNoSsl(args.GhesApiUrl, args.TargetUploadsUrl, args.GithubSourcePat) :
                sourceGithubApiFactory.Create(args.GhesApiUrl, args.TargetUploadsUrl, args.GithubSourcePat);

            var ghesVersionChecker = ghesVersionCheckerFactory.Create(sourceGithubApi);

            return new GenerateScriptCommandHandler(log, sourceGithubApi, versionProvider, ghesVersionChecker);
        }
    }
}
