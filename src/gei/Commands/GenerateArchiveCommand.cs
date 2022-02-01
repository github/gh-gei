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

        public GenerateArchiveCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-archive")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to source GHES API";

            var githubURL = new Option<string>("--github-url")
            {
                IsRequired = false
            };
            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var githubSourceRepo = new Option<string>("--github-source-repo")
            {
                IsRequired = true
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubURL);
            AddOption(githubSourceOrg);
            AddOption(githubSourceRepo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubURL, string githubSourceOrg, string githubSourceRepo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Migration Archive...");
            _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");

            if (string.IsNullOrWhiteSpace(githubURL))
            {
                _log.LogInformation("GitHub URL not provided, defaulting to https://api.github.com");
                githubURL = "https://api.github.com";
            }

            var repositories = new string[] { githubSourceRepo };

            var githubApi = _sourceGithubApiFactory.CreateClientNoSSL();
            var migrationId = await githubApi.StartArchiveGeneration(githubURL, githubSourceOrg, repositories);

            _log.LogInformation($"Archive generation started with id: {migrationId}");

            var isFinished = false;
            var timeOut = DateTime.Now.AddHours(10);

            while (!isFinished && DateTime.Now < timeOut)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(githubURL, githubSourceOrg, migrationId);
                var stringStatus = GithubEnums.ArchiveMigrationStatusToString(archiveStatus);
    
                _log.LogInformation($"Waiting for archive generation to finish. Current status: {stringStatus}");

                if (archiveStatus == GithubEnums.ArchiveMigrationStatus.Exported)
                {
                    isFinished = true;
                }
                else if (archiveStatus == GithubEnums.ArchiveMigrationStatus.Failed)
                {
                    _log.LogError($"Archive generation failed with id: {migrationId}");
                    return;
                }

                // 10 seconds
                await Task.Delay(10000);
            }

            var urlLocation = await githubApi.GetArchiveMigrationUrl(githubURL, githubSourceOrg, migrationId);
            _log.LogInformation($"Archive dowload url: {urlLocation}");
        }
    }
}
