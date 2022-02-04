using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly AdoApiFactory _sourceAdoApiFactory;

        public GenerateScriptCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, AdoApiFactory sourceAdoApiFactory) : base("generate-script")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _sourceAdoApiFactory = sourceAdoApiFactory;

            Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";

            var githubSourceOrgOption = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable. Will fall back to GH_PAT if not set."
            };
            var ghesUrlOption = new Option<string>("--ghes-url")
            {
                IsRequired = false,
                Description = "The base URL of your source GHES instance. For example: https://ghes.contoso.com"
            };
            var adoSourceOrgOption = new Option<string>("--ado-source-org")
            {
                IsRequired = false,
                Description = "Uses ADO_PAT env variable."
            };
            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };
            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1"))
            {
                IsRequired = false
            };
            var ssh = new Option("--ssh")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrgOption);
            AddOption(ghesUrlOption);
            AddOption(adoSourceOrgOption);
            AddOption(githubTargetOrgOption);
            AddOption(outputOption);
            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, FileInfo, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string ghesUrl, string adoSourceOrg, string githubTargetOrg, FileInfo output, bool ssh = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Script...");
            if (!string.IsNullOrWhiteSpace(githubSourceOrg))
            {
                _log.LogInformation($"GITHUB SOURCE ORG: {githubSourceOrg}");
            }
            if (!string.IsNullOrWhiteSpace(adoSourceOrg))
            {
                _log.LogInformation($"ADO SOURCE ORG: {adoSourceOrg}");
            }
            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"OUTPUT: {output}");
            if (ssh)
            {
                _log.LogInformation("SSH: true");
            }

            if (string.IsNullOrWhiteSpace(githubSourceOrg) && string.IsNullOrWhiteSpace(adoSourceOrg))
            {
                throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
            }

            var script = string.IsNullOrWhiteSpace(githubSourceOrg) ?
                await InvokeAdo(adoSourceOrg, githubTargetOrg, ssh) :
                await InvokeGithub(githubSourceOrg, ghesUrl, githubTargetOrg, ssh);

            if (output != null)
            {
                File.WriteAllText(output.FullName, script);
            }
        }

        private async Task<string> InvokeGithub(string githubSourceOrg, string ghesUrl, string githubTargetOrg, bool ssh)
        {
            var repos = await GetGithubRepos(_sourceGithubApiFactory.Create($"{ghesUrl}/api/v3"), githubSourceOrg);
            return GenerateGithubScript(repos, githubSourceOrg, ghesUrl, githubTargetOrg, ssh);
        }

        private async Task<string> InvokeAdo(string adoSourceOrg, string githubTargetOrg, bool ssh)
        {
            var repos = await GetAdoRepos(_sourceAdoApiFactory.Create(), adoSourceOrg);
            return GenerateAdoScript(repos, adoSourceOrg, githubTargetOrg, ssh);
        }

        public async Task<IEnumerable<string>> GetGithubRepos(GithubApi github, string githubOrg)
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

        public async Task<IDictionary<string, IEnumerable<string>>> GetAdoRepos(AdoApi adoApi, string adoOrg)
        {
            var repos = new Dictionary<string, IEnumerable<string>>();

            if (!string.IsNullOrWhiteSpace(adoOrg) && adoApi != null)
            {
                var teamProjects = await adoApi.GetTeamProjects(adoOrg);

                foreach (var teamProject in teamProjects)
                {
                    _log.LogInformation($"Team Project: {teamProject}");
                    var projectRepos = await adoApi.GetRepos(adoOrg, teamProject);
                    repos.Add(teamProject, projectRepos);

                    foreach (var repo in projectRepos)
                    {
                        _log.LogInformation($"  Repo: {repo}");
                    }
                }

                return repos;
            }

            throw new ArgumentException("All arguments must be non-null");
        }

        public string GenerateGithubScript(IEnumerable<string> repos, string githubSourceOrg, string ghesUrl, string githubTargetOrg, bool ssh)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

            foreach (var repo in repos)
            {
                content.AppendLine(MigrateGithubRepoScript(githubSourceOrg, ghesUrl, githubTargetOrg, repo, ssh));
            }

            return content.ToString();
        }

        public string GenerateAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoSourceOrg, string githubTargetOrg, bool ssh)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine($"# =========== Organization: {adoSourceOrg} ===========");

            foreach (var teamProject in repos.Keys)
            {
                content.AppendLine();
                content.AppendLine($"# === Team Project: {adoSourceOrg}/{teamProject} ===");

                if (!repos[teamProject].Any())
                {
                    content.AppendLine("# Skipping this Team Project because it has no git repos");
                }
                else
                {
                    foreach (var repo in repos[teamProject])
                    {
                        var githubRepo = GetGithubRepoName(teamProject, repo);
                        content.AppendLine(MigrateAdoRepoScript(adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, ssh));
                    }
                }
            }

            return content.ToString();
        }

        private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo.Replace(" ", "-")}";

        private string MigrateGithubRepoScript(string githubSourceOrg, string ghesUrl, string githubTargetOrg, string repo, bool ssh)
        {
            var ghesArg = string.IsNullOrWhitespace(ghesUrl) ? string.Empty : $" --ghes-url \"{ghesUrl}\"";
            return $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\"{ghesArg} --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)}";
        }

        private string MigrateAdoRepoScript(string adoSourceOrg, string teamProject, string adoRepo, string githubTargetOrg, string githubRepo, bool ssh)
        {
            return $"gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{teamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{githubRepo}\"{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)}";
        }
    }
}