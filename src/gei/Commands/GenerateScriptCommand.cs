using System;
using System.CommandLine;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommand : CommandBase<GenerateScriptCommandArgs, GenerateScriptCommandHandler>
    {
        public GenerateScriptCommand() : base(
                name: "generate-script",
                description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.")
        {
            AddOption(GithubSourceOrg);
            AddOption(AdoServerUrl);
            AddOption(AdoSourceOrg);
            AddOption(AdoTeamProject);
            AddOption(GithubTargetOrg);

            AddOption(GhesApiUrl);
            AddOption(AwsBucketName);
            AddOption(NoSslVerify);
            AddOption(DownloadMigrationLogs);

            AddOption(SkipReleases);
            AddOption(LockSourceRepo);

            AddOption(Output);
            AddOption(Sequential);
            AddOption(GithubSourcePat);
            AddOption(AdoPat);
            AddOption(Verbose);
        }
        public Option<string> GithubSourceOrg { get; } = new("--github-source-org")
        {
            Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT if not set."
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

        public Option<string> AdoPat { get; } = new("--ado-pat")
        {
            IsHidden = true
        };

        public Option<string> AwsBucketName { get; } = new("--aws-bucket-name")
        {
            Description = "If using AWS, the name of the S3 bucket to upload the BBS archive to."
        };

        public Option<bool> Verbose { get; } = new("--verbose");

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

            var sourceGithubApiFactory = sp.GetRequiredService<ISourceGithubApiFactory>();
            GithubApi sourceGithubApi = null;
            AdoApi sourceAdoApi = null;

            if (args.GithubSourceOrg.HasValue())
            {
                sourceGithubApi = args.GhesApiUrl.HasValue() && args.NoSslVerify ?
                    sourceGithubApiFactory.CreateClientNoSsl(args.GhesApiUrl, args.GithubSourcePat) :
                    sourceGithubApiFactory.Create(args.GhesApiUrl, args.GithubSourcePat);
            }
            else
            {
                var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
                sourceAdoApi = adoApiFactory.Create(args.AdoServerUrl, args.AdoPat);
            }

            return new GenerateScriptCommandHandler(log, sourceGithubApi, sourceAdoApi, versionProvider);
        }
    }

    public class GenerateScriptCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string AdoServerUrl { get; set; }
        public string AdoSourceOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubTargetOrg { get; set; }
        public FileInfo Output { get; set; }
        public string GhesApiUrl { get; set; }
        public string AwsBucketName { get; set; }
        public bool NoSslVerify { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool Sequential { get; set; }
        public string GithubSourcePat { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
