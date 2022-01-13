using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly Lazy<GithubApi> _lazyGithubApi;

        public GenerateScriptCommand(OctoLogger log, Lazy<GithubApi> lazyGithubApi) : base("generate-script")
        {
            _log = log;
            _lazyGithubApi = lazyGithubApi;

            Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";

            var githubSourceOrgOption = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true
            };
            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1"))
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrgOption);
            AddOption(githubTargetOrgOption);
            AddOption(outputOption);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, FileInfo, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string githubTargetOrg, FileInfo output, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Script...");
            _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"OUTPUT: {output}");

            var repos = await GetRepos(_lazyGithubApi.Value, githubSourceOrg);

            var script = GenerateScript(repos, githubSourceOrg, githubTargetOrg);

            if (output != null)
            {
                File.WriteAllText(output.FullName, script);
            }
        }

        public async Task<IEnumerable<string>> GetRepos(GithubApi github, string githubOrg)
        {
            if (!string.IsNullOrWhiteSpace(githubOrg) && github != null)
            {
                _log.LogInformation($"GITHUB ORG: {githubOrg}");
                var repos = await github.GetRepos(githubOrg);

                foreach (var repo in repos)
                {
                    _log.LogInformation($"    Repo: {repo}");
                }

                return repos;
            }

            throw new ArgumentException("All arguments must be non-null");
        }

        public string GenerateScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

            foreach (var repo in repos)
            {
                content.AppendLine(MigrateRepoScript(githubSourceOrg, githubTargetOrg, repo));
            }

            return content.ToString();
        }

        private string MigrateRepoScript(string githubSourceOrg, string githubTargetOrg, string repo)
        {
            return $"./gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(_log.Verbose ? " --verbose" : string.Empty)}";
        }
    }
}