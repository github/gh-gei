using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

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
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT if not set."
            };
            var adoSourceOrgOption = new Option<string>("--ado-source-org")
            {
                IsRequired = false,
                Description = "Uses ADO_PAT env variable or --ado-pat option."
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false
            };
            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true
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
                Description = "Required if migrating from GHES. The connection string for the Azure storage account, used to upload data archives pre-migration. For example: \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\""
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
            };
            var skipReleases = new Option("--skip-releases")
            {
                IsHidden = true,
                IsRequired = false,
                Description = "Extra setting to skip releases when migrating. Default value is false."
            };

            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1"))
            {
                IsRequired = false
            };
            var ssh = new Option("--ssh")
            {
                IsRequired = false,
                IsHidden = true
            };
            var sequential = new Option("--sequential")
            {
                IsRequired = false,
                Description = "Waits for each migration to finish before moving on to the next one."
            };
            var githubSourcePath = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var adoPat = new Option<string>("--ado-pat")
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

            AddOption(ghesApiUrl);
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);

            AddOption(skipReleases);

            AddOption(outputOption);
            AddOption(ssh);
            AddOption(sequential);
            AddOption(githubSourcePath);
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, FileInfo, string, string, bool, bool, bool, bool, string, string, bool>(Invoke);
        }

        public async Task Invoke(
          string githubSourceOrg,
          string adoSourceOrg,
          string adoTeamProject,
          string githubTargetOrg,
          FileInfo output,
          string ghesApiUrl = "",
          string azureStorageConnectionString = "",
          bool noSslVerify = false,
          bool skipReleases = false,
          bool ssh = false,
          bool sequential = false,
          string githubSourcePat = null,
          string adoPat = null,
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

            if (!string.IsNullOrWhiteSpace(adoTeamProject))
            {
                _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
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

            _log.LogInformation($"SKIP RELEASES: {skipReleases}");

            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"OUTPUT: {output}");
            if (ssh)
            {
                _log.LogWarning("SSH mode is no longer supported. --ssh flag will be ignored.");
            }
            if (sequential)
            {
                _log.LogInformation("SEQUENTIAL: true");
            }
            if (githubSourcePat is not null)
            {
                _log.LogInformation("GITHUB SOURCE PAT: ***");
            }
            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            if (string.IsNullOrWhiteSpace(githubSourceOrg) && string.IsNullOrWhiteSpace(adoSourceOrg))
            {
                throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
            }

            var script = string.IsNullOrWhiteSpace(githubSourceOrg) ?
                await InvokeAdo(adoSourceOrg, adoTeamProject, githubTargetOrg, sequential, adoPat) :
                await InvokeGithub(githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, sequential, githubSourcePat, skipReleases);

            if (output != null)
            {
                await File.WriteAllTextAsync(output.FullName, script);
            }
        }

        private async Task<string> InvokeGithub(string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool sequential, string githubSourcePat, bool skipReleases)
        {
            var repos = await GetGithubRepos(_sourceGithubApiFactory.Create(sourcePersonalAccessToken: githubSourcePat), githubSourceOrg);
            return sequential
                ? GenerateSequentialGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, skipReleases)
                : GenerateParallelGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, skipReleases);
        }

        private async Task<string> InvokeAdo(string adoSourceOrg, string adoTeamProject, string githubTargetOrg, bool sequential, string adoPat)
        {
            var repos = await GetAdoRepos(_sourceAdoApiFactory.Create(adoPat), adoSourceOrg, adoTeamProject);
            return sequential
                ? GenerateSequentialAdoScript(repos, adoSourceOrg, githubTargetOrg)
                : GenerateParallelAdoScript(repos, adoSourceOrg, githubTargetOrg);
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

        public string GenerateSequentialGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool skipReleases)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(@"#!/usr/bin/pwsh");

            content.AppendLine(EXEC_FUNCTION_BLOCK);

            content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

            foreach (var repo in repos)
            {
                content.AppendLine(Exec(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, azureStorageConnectionString, noSslVerify, true, skipReleases)));
            }

            return content.ToString();
        }

        public string GenerateParallelGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool skipReleases)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(EXEC_FUNCTION_BLOCK);
            content.AppendLine(EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);

            content.AppendLine();
            content.AppendLine("$Succeeded = 0");
            content.AppendLine("$Failed = 0");
            content.AppendLine("$RepoMigrations = [ordered]@{}");

            content.AppendLine();
            content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

            content.AppendLine();
            content.AppendLine("# === Queuing repo migrations ===");

            // Queuing migrations
            foreach (var repo in repos)
            {
                content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, azureStorageConnectionString, noSslVerify, false, skipReleases))}");
                content.AppendLine($"$RepoMigrations[\"{repo}\"] = $MigrationID");
                content.AppendLine();
            }

            // Waiting for migrations
            content.AppendLine();
            content.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {githubSourceOrg} ===========");
            content.AppendLine();

            // Query each migration's status
            foreach (var repo in repos)
            {
                content.AppendLine(WaitForMigrationScript(repo));
                content.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
                content.AppendLine();
            }

            // Generating the final report
            content.AppendLine();
            content.AppendLine("Write-Host =============== Summary ===============");
            content.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            content.AppendLine("Write-Host Total number of failed migrations: $Failed");

            content.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");

            content.AppendLine();
            content.AppendLine();

            return content.ToString();
        }

        public string GenerateSequentialAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoSourceOrg, string githubTargetOrg)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(EXEC_FUNCTION_BLOCK);

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
                        content.AppendLine(Exec(MigrateAdoRepoScript(adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, true)));
                    }
                }
            }

            return content.ToString();
        }

        public string GenerateParallelAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoSourceOrg, string githubTargetOrg)
        {
            if (repos == null)
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(EXEC_FUNCTION_BLOCK);
            content.AppendLine(EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);

            content.AppendLine();
            content.AppendLine("$Succeeded = 0");
            content.AppendLine("$Failed = 0");
            content.AppendLine("$RepoMigrations = [ordered]@{}");

            content.AppendLine();
            content.AppendLine($"# =========== Organization: {adoSourceOrg} ===========");

            // Queueing migrations
            foreach (var teamProject in repos.Keys)
            {
                content.AppendLine();
                content.AppendLine($"# === Queuing repo migrations for Team Project: {adoSourceOrg}/{teamProject} ===");

                if (!repos[teamProject].Any())
                {
                    content.AppendLine("# Skipping this Team Project because it has no git repos");
                    continue;
                }

                foreach (var repo in repos[teamProject])
                {
                    var githubRepo = GetGithubRepoName(teamProject, repo);
                    content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateAdoRepoScript(adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, false))}");
                    content.AppendLine($"$RepoMigrations[\"{githubRepo}\"] = $MigrationID");
                    content.AppendLine();
                }
            }

            // Waiting for migrations
            content.AppendLine();
            content.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoSourceOrg} ===========");

            // Query each migration's status
            foreach (var teamProject in repos.Keys)
            {
                if (repos[teamProject].Any())
                {
                    content.AppendLine();
                    content.AppendLine($"# === Migration stauts for Team Project: {adoSourceOrg}/{teamProject} ===");
                }

                foreach (var repo in repos[teamProject])
                {
                    var githubRepo = GetGithubRepoName(teamProject, repo);
                    content.AppendLine(WaitForMigrationScript(githubRepo));
                    content.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
                    content.AppendLine();
                }
            }

            // Generating the final report
            content.AppendLine();
            content.AppendLine("Write-Host =============== Summary ===============");
            content.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            content.AppendLine("Write-Host Total number of failed migrations: $Failed");

            content.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");

            content.AppendLine();
            content.AppendLine();

            return content.ToString();
        }

        private async Task<IEnumerable<string>> GetTeamProjectRepos(AdoApi adoApi, string adoOrg, string teamProject)
        {
            _log.LogInformation($"Team Project: {teamProject}");
            var projectRepos = await adoApi.GetEnabledRepos(adoOrg, teamProject);

            foreach (var repo in projectRepos)
            {
                _log.LogInformation($"  Repo: {repo}");
            }
            return projectRepos;
        }

        private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo.Replace(" ", "-")}";

        private string MigrateGithubRepoScript(string githubSourceOrg, string githubTargetOrg, string repo, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool wait, bool skipReleases)
        {
            var ghesRepoOptions = "";
            if (!string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                ghesRepoOptions = GetGhesRepoOptions(ghesApiUrl, azureStorageConnectionString, noSslVerify);
            }

            return $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(!string.IsNullOrEmpty(ghesRepoOptions) ? $" {ghesRepoOptions}" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)} {(skipReleases ? " --skip-releases" : string.Empty)}";
        }

        private string MigrateAdoRepoScript(string adoSourceOrg, string teamProject, string adoRepo, string githubTargetOrg, string githubRepo, bool wait) =>
            $"gh gei migrate-repo --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{teamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}";

        private string GetGhesRepoOptions(string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify)
        {
            return $"--ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\"{(noSslVerify ? " --no-ssl-verify" : string.Empty)}";
        }

        private string WaitForMigrationScript(string repoMigrationKey = null) => $"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

        private string Exec(string script) => Wrap(script, "Exec");

        private string ExecAndGetMigrationId(string script) => Wrap(script, "ExecAndGetMigrationID");

        private string Wrap(string script, string outerCommand = "") =>
            script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

        private const string EXEC_FUNCTION_BLOCK = @"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}";

        private const string EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK = @"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}";
    }
}
