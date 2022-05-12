using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Octoshift;
using System.IO;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class DownloadLogsCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;

        public DownloadLogsCommand(
            OctoLogger log,
            ITargetGithubApiFactory targetGithubApiFactory
        ) : base("download-logs")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;

            Description = "Downloads migration logs for migrations.";

            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Target GitHub organization to download logs from."
            };

            var targetRepo = new Option<string>("--target-repo")
            {
                IsRequired = true,
                Description = "Target repository to download latest log for."
            };

            var targetApiUrl = new Option<string>("--target-api-url")
            {
                IsRequired = false,
                Description = "Target GitHub API URL if not migrating to github.com (default: https://api.github.com)."
            };

            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false,
                Description = "Personal access token of the GitHub target.  Overrides GH_PAT environment variable."
            };

            var logFile = new Option<string>("--log-file")
            {
                IsRequired = false,
                Description = "Local file to write log to (default: migration-log-ORG-REPO.log)."
            };

            var overwrite = new Option("--overwrite")
            {
                IsRequired = false,
                Description = "Overwrite log file if it exists."
            };

            var verbose = new Option("--verbose")
            {
                IsRequired = false,
                Description = "Display more information to the console."
            };

            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(targetApiUrl);
            AddOption(githubTargetPat);
            AddOption(logFile);
            AddOption(overwrite);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubTargetOrg, string targetRepo, string targetApiUrl = null, string githubTargetPat = null, string logFile = null, bool overwrite = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation($"Downloading logs for organization {githubTargetOrg}...");

            if (githubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }

            logFile ??= $"migration-log-{githubTargetOrg}-{targetRepo}.log";

            if (File.Exists(logFile))
            {
                if (!overwrite)
                {
                    _log.LogError($"File {logFile} already exists!  Use --overwrite to overwite this file.");
                    return;
                }

                _log.LogWarning($"Overwriting {logFile} due to --overwrite option.");
            }

            _log.LogInformation($"Downloading log for repository {targetRepo} to {logFile}...");

            var githubApi = _targetGithubApiFactory.Create(targetApiUrl, githubTargetPat);
            var logUrl = await githubApi.GetMigrationLogUrl(githubTargetOrg, targetRepo);

            if (logUrl == null)
            {
                _log.LogError($"Migration not found for repository {targetRepo}!");
                return;
            }

            if (logUrl.Length == 0)
            {
                _log.LogError($"Migration log not available for migration for repository {targetRepo}!");
                return;
            }

            var downloadSuccessful = await HttpDownloadService.Download(logUrl, logFile);

            if (!downloadSuccessful)
            {
                _log.LogError($"Could not download log for repository {targetRepo}!");
                return;
            }

            _log.LogInformation($"Downloaded {targetRepo} log to {logFile}.");
        }
    }
}
