﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrationOrgCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

        public MigrationOrgCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("migrate-repo")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub APIs to migrate a GitHub org with its teams and the repositories.";

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-target-pat option."
            };
            var ssh = new Option("--ssh")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Uses SSH protocol instead of HTTPS to push a Git repository into the target repository on GitHub."
            };
            var githubSourcePat = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(githubTargetOrg);

            AddOption(ssh);
            AddOption(githubSourcePat);
            AddOption(githubTargetPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<MigrationOrgCommandArgs>(Invoke);
        }

        public async Task Invoke(MigrationOrgCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            LogAndValidateOptions(args);

            var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: args.GithubTargetPat);

            var githubOrgId = await githubApi.GetOrganizationId(args.GithubTargetOrg);
            var sourceOrgUrl = GetGithubOrgUrl(args.GithubSourceOrg, null);
            var sourceToken = GetSourceToken(args);
            var targetToken = args.GithubTargetPat ?? _environmentVariableProvider.TargetGithubPersonalAccessToken();

            var migrationId = await githubApi.StartOrganizationMigration(
                sourceOrgUrl,
                args.GithubTargetOrg,
                githubOrgId,
                sourceToken,
                targetToken);

            _log.LogSuccess($"Org Migration has been initiated (ID: {migrationId}).");
        }

        private string GetSourceToken(MigrationOrgCommandArgs args) => 
            args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

        private string GetGithubOrgUrl(string org, string baseUrl) => $"{baseUrl ?? DEFAULT_GITHUB_BASE_URL}/{org}".Replace(" ", "%20");

        private void LogAndValidateOptions(MigrationOrgCommandArgs args)
        {
            _log.LogInformation("Migrating Org...");
            _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
            _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");

            if (args.Ssh)
            {
                _log.LogWarning("SSH mode is no longer supported. --ssh flag will be ignored");
            }

            if (args.GithubSourcePat is not null)
            {
                _log.LogInformation("GITHUB SOURCE PAT: ***");
            }

            if (args.GithubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");

                if (args.GithubSourcePat is null)
                {
                    args.GithubSourcePat = args.GithubTargetPat;
                    _log.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
                }
            }
        }
    }

    public class MigrationOrgCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string GithubTargetOrg { get; set; }
        public bool Ssh { get; set; }
        public bool Verbose { get; set; }
        public string GithubSourcePat { get; set; }
        public string GithubTargetPat { get; set; }
    }
}
