using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Runtime.CompilerServices;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommand : Command
    {
        public GenerateScriptCommand(
            OctoLogger log,
            ISourceGithubApiFactory sourceGithubApiFactory,
            AdoApiFactory sourceAdoApiFactory,
            EnvironmentVariableProvider environmentVariableProvider,
            IVersionProvider versionProvider) : base(
                name: "generate-script",
                description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.")
        {
            var githubSourceOrgOption = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT if not set."
            };
            var adoServerUrlOption = new Option<string>("--ado-server-url")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
            };
            var adoSourceOrgOption = new Option<string>("--ado-source-org")
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
            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true
            };

            // GHES migration path
            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The api endpoint for the hostname of your GHES instance. For example: http(s)://myghes.com/api/v3"
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The connection string for the Azure storage account, used to upload data archives pre-migration. For example: \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\""
            };
            var noSslVerify = new Option<bool>("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
            };
            var skipReleases = new Option<bool>("--skip-releases")
            {
                IsRequired = false,
                Description = "Skip releases when migrating."
            };
            var lockSourceRepo = new Option<bool>("--lock-source-repo")
            {
                IsRequired = false,
                Description = "Lock the source repository when migrating."
            };

            var downloadMigrationLogs = new Option<bool>("--download-migration-logs")
            {
                IsRequired = false,
                Description = "Downloads the migration log for each repository migration."
            };

            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1"))
            {
                IsRequired = false
            };
            var sequential = new Option<bool>("--sequential")
            {
                IsRequired = false,
                Description = "Waits for each migration to finish before moving on to the next one."
            };
            var githubSourcePath = new Option<string>("--github-source-pat")
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

            AddOption(githubSourceOrgOption);
            AddOption(adoServerUrlOption);
            AddOption(adoSourceOrgOption);
            AddOption(adoTeamProject);
            AddOption(githubTargetOrgOption);

            AddOption(ghesApiUrl);
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);
            AddOption(downloadMigrationLogs);

            AddOption(skipReleases);
            AddOption(lockSourceRepo);

            AddOption(outputOption);
            AddOption(sequential);
            AddOption(githubSourcePath);
            AddOption(adoPat);
            AddOption(verbose);

            var handler = new GenerateScriptCommandHandler(
                log,
                sourceGithubApiFactory,
                sourceAdoApiFactory,
                environmentVariableProvider,
                versionProvider);
            Handler = CommandHandler.Create<GenerateScriptCommandArgs>(handler.Invoke);
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
        public string AzureStorageConnectionString { get; set; }
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
