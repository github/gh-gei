using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateArchiveCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;

        private const int Timeout_In_Hours = 10;
        private const int Delay_In_Milliseconds = 10000; // 10 seconds

        public GenerateArchiveCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory) : base("generate-archive")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to the source GHES API.";

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var ghesSourceUrl = new Option<string>("--ghes-source-url")
            {
                IsRequired = false,
                Description = "The base URL of your source GHES instance.For example: https://ghes.contoso.com"
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(ghesSourceUrl);
            AddOption(sourceRepo);
            AddOption(noSslVerify);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string ghesSourceUrl, string sourceRepo, bool noSslVerify = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Migration Archives...");
            _log.LogInformation($"GHES SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"GHES SOURCE REPO: {sourceRepo}");

            if (string.IsNullOrWhiteSpace(ghesSourceUrl))
            {
                _log.LogInformation("--ghes-api-url not provided, defaulting to https://api.github.com");
                ghesSourceUrl = "https://api.github.com";
            }
            else
            {
                _log.LogInformation($"GHES API URL: {ghesSourceUrl}");
            }

            if (noSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            var githubSourceApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSSL(ghesSourceUrl) : _sourceGithubApiFactory.Create(ghesSourceUrl);

            var gitDataArchiveId = await githubSourceApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await githubSourceApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var metadataArchiveUrl = await WaitForArchiveGeneration(githubSourceApi, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive(metadata) download url: {metadataArchiveUrl}");
            var gitArchiveUrl = await WaitForArchiveGeneration(githubSourceApi, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive(git) download url: {gitArchiveUrl}");
        }

        private async Task<string> WaitForArchiveGeneration(GithubApi githubApi, string githubSourceOrg, int archiveId)
        {
            var timeOut = DateTime.Now.AddHours(Timeout_In_Hours);
            while (DateTime.Now < timeOut)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(githubSourceOrg, archiveId);
                _log.LogInformation($"Waiting for archive with id {archiveId} generation to finish. Current status: {archiveStatus}");
                if (archiveStatus == ArchiveMigrationStatus.Exported)
                {
                    return await githubApi.GetArchiveMigrationUrl(githubSourceOrg, archiveId);
                }
                if (archiveStatus == ArchiveMigrationStatus.Failed)
                {
                    throw new OctoshiftCliException($"Archive generation failed with id: {archiveId}");
                }
                await Task.Delay(Delay_In_Milliseconds);
            }
            throw new TimeoutException($"Archive generation timed out after {Timeout_In_Hours} hours");
        }
    }
}
