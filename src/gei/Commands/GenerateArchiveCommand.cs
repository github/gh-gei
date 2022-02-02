using System;
using System.Collections.Generic;
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

        private const int _timeoutInHours = 10;
        private const int _delayInMS = 10000; // 10 seconds

        public GenerateArchiveCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-archive")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to the source GHES API.";

            var ghesUrl = new Option<string>("--ghes-url")
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

            AddOption(ghesUrl);
            AddOption(githubSourceOrg);
            AddOption(githubSourceRepo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string ghesUrl, string githubSourceOrg, string githubSourceRepo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Migration Archive...");
            _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");

            if (string.IsNullOrWhiteSpace(ghesUrl))
            {
                _log.LogInformation("ghesUrl not provided, defaulting to https://api.github.com");
                ghesUrl = "https://api.github.com";
            }

            var githubApi = _sourceGithubApiFactory.CreateClientNoSSL();

            var repositories = new string[] { githubSourceRepo };

            // Archive of repo git data
            var gitDataOptions = new
            {
                repositories,
                exclude_metadata = true
            };
            var gitDataArchiveId = await githubApi.StartArchiveGeneration(ghesUrl, githubSourceOrg, gitDataOptions);

            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");

            // Archive of repo metadata
            var metadataOptions = new
            {
                repositories,
                exclude_git_data = true,
                exclude_releases = true,
                exclude_owner_projects = true
            };
            var metadataArchiveId = await githubApi.StartArchiveGeneration(ghesUrl, githubSourceOrg, metadataOptions);

            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");


            var ids = new int[] { gitDataArchiveId, metadataArchiveId };

            var isFinished = new Dictionary<int, bool>()
            {
                { gitDataArchiveId, false },
                { metadataArchiveId, false }
            };

            var currFinished = 0;
            var total = ids.Length;
            var timeOut = DateTime.Now.AddHours(_timeoutInHours);

            while (currFinished < total && DateTime.Now < timeOut)
            {
                foreach (var pair in isFinished)
                {
                    if (pair.Value)
                    {
                        continue;
                    }
                    var id = pair.Key;
                    var archiveStatus = await githubApi.GetArchiveMigrationStatus(ghesUrl, githubSourceOrg, id);
                    var stringStatus = GithubEnums.ArchiveMigrationStatusToString(archiveStatus);

                    _log.LogInformation($"Waiting for archive with id {id} generation to finish. Current status: {stringStatus}");

                    if (archiveStatus == GithubEnums.ArchiveMigrationStatus.Exported)
                    {
                        isFinished[id] = true;
                        currFinished++;
                    }
                    else if (archiveStatus == GithubEnums.ArchiveMigrationStatus.Failed)
                    {
                        _log.LogError($"Archive generation failed with id: {id}");
                        return;
                    }

                }

                await Task.Delay(_delayInMS);
            }

            foreach(int id in ids) {
                var urlLocation = await githubApi.GetArchiveMigrationUrl(ghesUrl, githubSourceOrg, id);
                _log.LogInformation($"Archive dowload url: {urlLocation}");
            }
        }
    }
}
