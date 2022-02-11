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
            var adoSourceOrgOption = new Option<string>("--ado-source-org")
            {
                IsRequired = false,
                Description = "Uses ADO_PAT env variable."
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false
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
            AddOption(adoSourceOrgOption);
            AddOption(adoTeamProject);
            AddOption(githubTargetOrgOption);
            AddOption(outputOption);
            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, FileInfo, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubSourceOrg, string adoSourceOrg, string adoTeamProject, string githubTargetOrg, FileInfo output, bool ssh = false, bool verbose = false)
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
                _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
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
                await InvokeAdo(adoSourceOrg, adoTeamProject, githubTargetOrg, ssh) :
                await InvokeGithub(githubSourceOrg, githubTargetOrg, ssh);

            if (output != null)
            {
                File.WriteAllText(output.FullName, script);
            }
        }

        private async Task<string> InvokeGithub(string githubSourceOrg, string githubTargetOrg, bool ssh)
        {
            var targetApiUrl = "https://api.github.com";
            var repos = await GetGithubRepos(_sourceGithubApiFactory.Create(targetApiUrl), githubSourceOrg);
            return GenerateGithubScript(repos, githubSourceOrg, githubTargetOrg, ssh);
        }

        private async Task<string> InvokeAdo(string adoSourceOrg, string adoTeamProject, string githubTargetOrg, bool ssh)
        {
            var repos = await GetAdoRepos(_sourceAdoApiFactory.Create(), adoSourceOrg, adoTeamProject);
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

        public async Task<IDictionary<string, IEnumerable<string>>> GetAdoRepos(AdoApi adoApi, string adoOrg, string adoTeamProject)
        {
            var repos = new Dictionary<string, IEnumerable<string>>();

            if (!string.IsNullOrWhiteSpace(adoOrg) && adoApi != null)
            {
                var teamProjects = await adoApi.GetTeamProjects(adoOrg);
                if (string.IsNullOrEmpty(adoTeamProject))
                {
                    foreach (var teamProject in teamProjects)
                    {
                        var projectRepos = await GetTeamProjectRepos(adoApi, adoOrg, teamProject);
                        repos.Add(teamProject, projectRepos);
                    }
                }
                else
                {
                    if (teamProjects.Any(o => o.Equals(adoTeamProject, StringComparison.OrdinalIgnoreCase)))
                    {
                        var projectRepos = await GetTeamProjectRepos(adoApi, adoOrg, adoTeamProject);
                        repos.Add(adoTeamProject, projectRepos);
                    }
                }

                return repos;
            }

            throw new ArgumentException("All arguments must be non-null");
        }

        private async Task<IEnumerable<string>> GetTeamProjectRepos(AdoApi adoApi, string adoOrg, string teamProject)
        {
            _log.LogInformation($"Team Project: {teamProject}");
            var projectRepos = await adoApi.GetRepos(adoOrg, teamProject);

            foreach (var repo in projectRepos)
            {
                _log.LogInformation($"  Repo: {repo}");
            }
            return projectRepos;
        }

        public string GenerateGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, bool ssh)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");

            content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

            foreach (var repo in repos)
            {
                content.AppendLine(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ssh));
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

            content.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");

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

        private string MigrateGithubRepoScript(string githubSourceOrg, string githubTargetOrg, string repo, bool ssh)
        {
            return $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string MigrateAdoRepoScript(string adoSourceOrg, string teamProject, string adoRepo, string githubTargetOrg, string githubRepo, bool ssh)
        {
            return $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{teamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{githubRepo}\"{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }
    }
}
