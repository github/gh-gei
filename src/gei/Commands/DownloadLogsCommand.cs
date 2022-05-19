using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class DownloadLogsCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly HttpDownloadService _httpDownloadService;

        internal Func<string, bool> FileExists = path => File.Exists(path);

        public DownloadLogsCommand(
            OctoLogger log,
            ITargetGithubApiFactory targetGithubApiFactory,
            HttpDownloadService httpDownloadService
        ) : base("download-logs")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _httpDownloadService = httpDownloadService;

            Description = "Downloads migration logs for migrations.";
            IsHidden = true;

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
                Description = "Target GitHub API URL if not targeting github.com (default: https://api.github.com)."
            };

            var githubTargetPat = new Option<string>("--github-target-pat")
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

            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(targetApiUrl);
            AddOption(githubTargetPat);
            AddOption(migrationLogFile);
            AddOption(overwrite);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(
            string githubTargetOrg,
            string targetRepo,
            string targetApiUrl = null,
            string githubTargetPat = null,
            string migrationLogFile = null,
            bool overwrite = false,
            bool verbose = false
        )
        {
            _log.Verbose = verbose;

            _log.LogInformation($"Downloading logs for organization {githubTargetOrg}...");

            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"TARGET REPO: {targetRepo}");

            if (targetApiUrl.HasValue())
            {
                _log.LogInformation($"TARGET API URL: {targetApiUrl}");
            }

            if (githubTargetPat.HasValue())
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }

            if (migrationLogFile.HasValue())
            {
                _log.LogInformation($"MIGRATION LOG FILE: {migrationLogFile}");
            }

            migrationLogFile ??= $"migration-log-{githubTargetOrg}-{targetRepo}.log";

            if (FileExists(migrationLogFile))
            {
                if (!overwrite)
                {
                    throw new OctoshiftCliException($"File {migrationLogFile} already exists!  Use --overwrite to overwite this file.");
                }

                _log.LogWarning($"Overwriting {migrationLogFile} due to --overwrite option.");
            }

            _log.LogInformation($"Downloading log for repository {targetRepo} to {migrationLogFile}...");

            var githubApi = _targetGithubApiFactory.Create(targetApiUrl, githubTargetPat);
            var logUrl = await githubApi.GetMigrationLogUrl(githubTargetOrg, targetRepo);

            if (logUrl == null)
            {
                throw new OctoshiftCliException($"Migration not found for repository {targetRepo}!");
            }

            if (!logUrl.HasValue())
            {
                throw new OctoshiftCliException($"Migration found for repository {targetRepo}, but migration log not available yet!");
            }

            await _httpDownloadService.Download(logUrl, migrationLogFile);

            _log.LogSuccess($"Downloaded {targetRepo} log to {migrationLogFile}.");
        }
    }
}
