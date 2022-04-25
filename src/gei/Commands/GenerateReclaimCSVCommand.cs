﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateReclaimCSVCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;

        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GenerateReclaimCSVCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-reclaim-csv")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Generates a CSV with unreclained mannequins to reclaim them in bulk.";

            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };

            var outputOption = new Option<string>("--output")
            {
                IsRequired = true,
                Description = "Output filename"
            };

            var forceOption = new Option("--include-reclaimed")
            {
                IsRequired = false,
                Description = "Map the user even if it was previously mapped"
            };

            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubTargetOrgOption);
            AddOption(outputOption);
            AddOption(forceOption);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(
          string githubTargetOrg,
          string output,
          bool includeReclaimed = false,
          bool verbose = false)
        {
            const string targetApiUrl = "https://api.github.com";

            _log.Verbose = verbose;

            _log.LogInformation("Generating CSV...");

            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"FILE: {output}");
            _log.LogInformation($"INCLUDE RECLAIMED: {includeReclaimed}");

            var githubApi = _targetGithubApiFactory.Create(targetApiUrl);

            var githubOrgId = await githubApi.GetOrganizationId(githubTargetOrg);
            _log.LogInformation($"    Organization Id: {githubOrgId}");

            var mannequins = await githubApi.GetMannequins(githubOrgId);

            _log.LogInformation($"    # Mannequins Found: {mannequins.Count()}");

            var numberMannequins = 0;
            var contents = "login,claimantlogin\n";
            foreach (var mannequin in mannequins)
            {
                if (mannequin.MappedUser != null && !includeReclaimed)
                {
                    continue;
                }
                numberMannequins++;
                contents += $"{mannequin.Login},{mannequin.MappedUser?.Login}\n";
            }

            await WriteToFile(output, contents);

            _log.LogInformation($"    # Mannequins Included: {numberMannequins}");
        }
    }
}
