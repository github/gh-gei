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
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        private const int Timeout_In_Hours = 10;
        private const int Delay_In_Milliseconds = 10000; // 10 seconds

        public GenerateArchiveCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-archive")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to the source GHES API. For GHES, we expect that --ghes-api-url is passed in as the api endpoint for the hostname of your GHES instance. For example: https://api.myghes.com";

            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false
            };
            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
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

            AddOption(ghesApiUrl);
            AddOption(githubSourceOrg);
            AddOption(sourceRepo);
            AddOption(noSslVerify);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string ghesApiUrl, string githubSourceOrg, string sourceRepo, bool noSslVerify = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Migration Archives...");
            _log.LogInformation($"GHES SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"GHES SOURCE REPO: {sourceRepo}");

            if (string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                _log.LogInformation("--ghes-api-url not provided, defaulting to https://api.github.com");
                ghesApiUrl = "https://api.github.com";
            }

            if (noSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            var githubApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSSL() : _sourceGithubApiFactory.Create();

            var gitDataArchiveId = await githubApi.StartGitArchiveGeneration(ghesApiUrl, githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await githubApi.StartMetadataArchiveGeneration(ghesApiUrl, githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var metadataArchiveUrl = await WaitForArchiveGeneration(githubApi, ghesApiUrl, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive(metadata) download url: {metadataArchiveUrl}");
            var gitArchiveUrl = await WaitForArchiveGeneration(githubApi, ghesApiUrl, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive(git) download url: {gitArchiveUrl}");
        }

        private async Task<string> WaitForArchiveGeneration(GithubApi githubApi, string ghesApiUrl, string githubSourceOrg, int archiveId)
        {
            var timeOut = DateTime.Now.AddHours(Timeout_In_Hours);
            while (DateTime.Now < timeOut)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(ghesApiUrl, githubSourceOrg, archiveId);
                _log.LogInformation($"Waiting for archive with id {archiveId} generation to finish. Current status: {archiveStatus}");
                if (archiveStatus == ArchiveMigrationStatus.Exported)
                {
                    return await githubApi.GetArchiveMigrationUrl(ghesApiUrl, githubSourceOrg, archiveId);
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