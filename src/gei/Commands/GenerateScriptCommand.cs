﻿using System;
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
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GenerateScriptCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, AdoApiFactory sourceAdoApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-script")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _sourceAdoApiFactory = sourceAdoApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

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
            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };

            // GHES migration path
            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The api endpoint for the hostname of your GHES instance. For example: http(s)://api.myghes.com"
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The connection string for the Azure storage account, used to upload data archives pre-migration. For example: DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
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
            AddOption(githubTargetOrgOption);

            AddOption(ghesApiUrl);
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);

            AddOption(outputOption);
            AddOption(ssh);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, FileInfo, string, string, bool, bool, bool>(Invoke);
        }

        public async Task Invoke(
          string githubSourceOrg,
          string adoSourceOrg,
          string githubTargetOrg,
          FileInfo output,
          string ghesApiUrl = "",
          string azureStorageConnectionString = "",
          bool noSslVerify = false,
          bool ssh = false,
          bool verbose = false)
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

            // GHES Migration Path
            if (!string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                _log.LogInformation($"GHES API URL: {ghesApiUrl}");

                if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
                {
                    _log.LogInformation("--azure-storage-connection-string not set, using environment variable AZURE_STORAGE_CONNECTION_STRING");
                    azureStorageConnectionString = _environmentVariableProvider.AzureStorageConnectionString();

                    if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
                    {
                        throw new OctoshiftCliException("Please set either --azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING");
                    }
                }

                if (noSslVerify)
                {
                    _log.LogInformation("SSL verification disabled");
                }
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
                await InvokeGithub(githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, ssh);

            if (output != null)
            {
                File.WriteAllText(output.FullName, script);
            }
        }

        private async Task<string> InvokeGithub(string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool ssh)
        {
            var targetApiUrl = "https://api.github.com";
            var repos = await GetGithubRepos(_sourceGithubApiFactory.Create(targetApiUrl), githubSourceOrg);
            return GenerateGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, ssh);
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

        public string GenerateGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool ssh)
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
                content.AppendLine(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, azureStorageConnectionString, noSslVerify, ssh));
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

        private string MigrateGithubRepoScript(string githubSourceOrg, string githubTargetOrg, string repo, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool ssh)
        {
            var ghesRepoOptions = "";
            if (!string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                ghesRepoOptions = GHESRepoOptions(ghesApiUrl, azureStorageConnectionString, noSslVerify);
            }

            return $"Exec {{ gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(!string.IsNullOrEmpty(ghesRepoOptions) ? $" {ghesRepoOptions}" : string.Empty)}{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string MigrateAdoRepoScript(string adoSourceOrg, string teamProject, string adoRepo, string githubTargetOrg, string githubRepo, bool ssh)
        {
            return $"Exec {{ gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{teamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{githubRepo}\"{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string GHESRepoOptions(string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify)
        {
            return $"--ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\"{(noSslVerify ? " --no-ssl-verify" : string.Empty)}";
        }
    }
}
