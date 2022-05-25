using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class DownloadLogsCommand : Command
    {
        internal const int WaitIntervalInSeconds = 10;
        internal const int RetryMaxCount = 6;

        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;
        private readonly HttpDownloadService _httpDownloadService;

        internal Func<string, bool> FileExists = path => File.Exists(path);
        internal Func<int, Task> Delay = milliSeconds => Task.Delay(milliSeconds);

        public DownloadLogsCommand(
            OctoLogger log,
            GithubApiFactory githubApiFactory,
            HttpDownloadService httpDownloadService
        ) : base("download-logs")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _httpDownloadService = httpDownloadService;

            Description = "Downloads migration logs for migrations.";

            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true,
                Description = "GitHub organization to download logs from."
            };

            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true,
                Description = "Target repository to download latest log for."
            };

            var githubApiUrl = new Option<string>("--github-api-url")
            {
                IsRequired = false,
                Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
            };

            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false,
                Description = "Personal access token of the GitHub target.  Overrides GH_PAT environment variable."
            };

            var migrationLogFile = new Option<string>("--migration-log-file")
            {
                IsRequired = false,
                Description = "Local file to write migration log to (default: migration-log-ORG-REPO.log)."
            };

            var overwrite = new Option("--overwrite")
            {
                IsRequired = false,
                Description = "Overwrite migration log file if it exists."
            };

            var verbose = new Option("--verbose")
            {
                IsRequired = false,
                Description = "Display more information to the console."
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(githubApiUrl);
            AddOption(githubPat);
            AddOption(migrationLogFile);
            AddOption(overwrite);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(
            string githubOrg,
            string githubRepo,
            string githubApiUrl = null,
            string githubPat = null,
            string migrationLogFile = null,
            bool overwrite = false,
            bool verbose = false
        )
        {
            _log.Verbose = verbose;

            _log.LogInformation($"Downloading logs for organization {githubOrg}...");

            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");

            if (githubApiUrl.HasValue())
            {
                _log.LogInformation($"GITHUB API URL: {githubApiUrl}");
            }

            if (githubPat.HasValue())
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            if (migrationLogFile.HasValue())
            {
                _log.LogInformation($"MIGRATION LOG FILE: {migrationLogFile}");
            }

            migrationLogFile ??= $"migration-log-{githubOrg}-{githubRepo}.log";

            if (FileExists(migrationLogFile))
            {
                if (!overwrite)
                {
                    throw new OctoshiftCliException($"File {migrationLogFile} already exists!  Use --overwrite to overwite this file.");
                }

                _log.LogWarning($"Overwriting {migrationLogFile} due to --overwrite option.");
            }

            var githubApi = _githubApiFactory.Create(githubApiUrl, githubPat);

            for (var attempt = 1; attempt <= RetryMaxCount; attempt++)
            {
                var logUrl = await githubApi.GetMigrationLogUrl(githubOrg, githubRepo) ?? throw new OctoshiftCliException($"Migration for repository {githubRepo} not found!");

                if (logUrl.HasValue())
                {
                    _log.LogInformation($"Downloading log for repository {githubRepo} to {migrationLogFile}...");
                    await _httpDownloadService.Download(logUrl, migrationLogFile);

                    _log.LogSuccess($"Downloaded {githubRepo} log to {migrationLogFile}.");
                    break;
                }

                if (attempt == RetryMaxCount)
                {
                    throw new OctoshiftCliException($"Migration log for repository {githubRepo} still unavailable after one minute!");
                }

                _log.LogInformation($"Waiting {WaitIntervalInSeconds} more seconds for log to populate...");
                await Delay(WaitIntervalInSeconds * 1000);
            }
        }
    }
}
