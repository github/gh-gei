using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly AdoApiFactory _sourceAdoApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly IVersionProvider _versionProvider;

        public GenerateScriptCommand(
            OctoLogger log,
            ISourceGithubApiFactory sourceGithubApiFactory,
            AdoApiFactory sourceAdoApiFactory,
            EnvironmentVariableProvider environmentVariableProvider,
            IVersionProvider versionProvider) : base("generate-script")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _sourceAdoApiFactory = sourceAdoApiFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _versionProvider = versionProvider;

            Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";

            var githubSourceOrgOption = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT if not set."
            };
            var adoServerUrlOption = new Option<string>("--ado-server-url")
            {
                IsRequired = false,
                IsHidden = true,
                Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
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
            var archiveGhRepos = new Option("--archive-gh-repos")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GitHub Cloud. This will place the source repository into an archive state to prevent changes while the migration is taking place."
            };

            // GHES migration path
            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The api endpoint for the hostname of your GHES instance. For example: http(s)://myghes.com/api/v3"
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
                Description = "Skip releases when migrating."
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
            AddOption(adoServerUrlOption);
            AddOption(adoSourceOrgOption);
            AddOption(adoTeamProject);
            AddOption(githubTargetOrgOption);
            AddOption(archiveGhRepos);

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

            Handler = CommandHandler.Create<GenerateScriptCommandArgs>(Invoke);
        }

        public async Task Invoke(GenerateScriptCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Generating Script...");
            if (args.GithubSourceOrg.HasValue())
            {
                _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
            }
            if (args.AdoServerUrl.HasValue())
            {
                _log.LogInformation($"ADO SERVER URL: {args.AdoServerUrl}");
            }
            if (args.AdoSourceOrg.HasValue())
            {
                _log.LogInformation($"ADO SOURCE ORG: {args.AdoSourceOrg}");
            }

            if (args.AdoTeamProject.HasValue())
            {
                _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            }

            // GHES Migration Path
            if (args.GhesApiUrl.HasValue())
            {
                _log.LogInformation($"GHES API URL: {args.GhesApiUrl}");

                if (args.AzureStorageConnectionString.IsNullOrWhiteSpace())
                {
                    _log.LogInformation("--azure-storage-connection-string not set, using environment variable AZURE_STORAGE_CONNECTION_STRING");
                    args.AzureStorageConnectionString = _environmentVariableProvider.AzureStorageConnectionString();

                    if (args.AzureStorageConnectionString.IsNullOrWhiteSpace())
                    {
                        throw new OctoshiftCliException("Please set either --azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING");
                    }
                }

                if (args.NoSslVerify)
                {
                    _log.LogInformation("SSL verification disabled");
                }
            }

            if (args.SkipReleases)
            {
                _log.LogInformation("SKIP RELEASES: true");
            }

            _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");
            _log.LogInformation($"OUTPUT: {args.Output}");
            if (args.Ssh)
            {
                _log.LogWarning("SSH mode is no longer supported. --ssh flag will be ignored.");
            }
            if (args.Sequential)
            {
                _log.LogInformation("SEQUENTIAL: true");
            }
            if (args.GithubSourcePat is not null)
            {
                _log.LogInformation("GITHUB SOURCE PAT: ***");
            }
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            if (args.GithubSourceOrg.IsNullOrWhiteSpace() && args.AdoSourceOrg.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
            }

            if (args.AdoServerUrl.HasValue() && !args.AdoSourceOrg.HasValue())
            {
                throw new OctoshiftCliException("Must specify --ado-source-org with the collection name when using --ado-server-url");
            }

            var script = args.GithubSourceOrg.IsNullOrWhiteSpace() ?
                await InvokeAdo(args.AdoServerUrl, args.AdoSourceOrg, args.AdoTeamProject, args.GithubTargetOrg, args.Sequential, args.AdoPat) :
                await InvokeGithub(args.GithubSourceOrg, args.GithubTargetOrg, args.GhesApiUrl, args.AzureStorageConnectionString, args.NoSslVerify, args.Sequential, args.GithubSourcePat, args.SkipReleases, args.ArchiveGhRepos);

            if (args.Output.HasValue())
            {
                await WriteToFile(args.Output.FullName, script);
            }
        }

        private async Task<string> InvokeGithub(string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool sequential, string githubSourcePat, bool skipReleases, bool archiveRepos)
        {
            var repos = await GetGithubRepos(_sourceGithubApiFactory.Create(ghesApiUrl, githubSourcePat), githubSourceOrg);
            return sequential
                ? GenerateSequentialGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, skipReleases, archiveRepos)
                : GenerateParallelGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, azureStorageConnectionString, noSslVerify, skipReleases, archiveRepos);
        }

        private async Task<string> InvokeAdo(string adoServerUrl, string adoSourceOrg, string adoTeamProject, string githubTargetOrg, bool sequential, string adoPat)
        {
            var repos = await GetAdoRepos(_sourceAdoApiFactory.Create(adoServerUrl, adoPat), adoSourceOrg, adoTeamProject);
            return sequential
                ? GenerateSequentialAdoScript(repos, adoServerUrl, adoSourceOrg, githubTargetOrg)
                : GenerateParallelAdoScript(repos, adoServerUrl, adoSourceOrg, githubTargetOrg);
        }

        private async Task<IEnumerable<string>> GetGithubRepos(GithubApi github, string githubOrg)
        {
            if (githubOrg.IsNullOrWhiteSpace() || github is null)
            {
                throw new ArgumentException("All arguments must be non-null");
            }

            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            var repos = await github.GetRepos(githubOrg);

            foreach (var repo in repos)
            {
                _log.LogInformation($"    Repo: {repo}");
            }

            return repos;
        }

        private async Task<IDictionary<string, IEnumerable<string>>> GetAdoRepos(AdoApi adoApi, string adoOrg, string adoTeamProject)
        {
            if (adoOrg.IsNullOrWhiteSpace() || adoApi is null)
            {
                throw new ArgumentException("All arguments must be non-null");
            }

            var repos = new Dictionary<string, IEnumerable<string>>();

            var teamProjects = await adoApi.GetTeamProjects(adoOrg);
            if (adoTeamProject.HasValue())
            {
                teamProjects = teamProjects.Any(o => o.Equals(adoTeamProject, StringComparison.OrdinalIgnoreCase))
                    ? new[] { adoTeamProject }
                    : Enumerable.Empty<string>();
            }

            foreach (var teamProject in teamProjects)
            {
                var projectRepos = await GetTeamProjectRepos(adoApi, adoOrg, teamProject);
                repos.Add(teamProject, projectRepos);
            }

            return repos;
        }

        private string GenerateSequentialGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool skipReleases, bool archiveRepos)
        {
            if (!repos.Any())
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(PWSH_SHEBANG);
            content.AppendLine();
            content.AppendLine(VersionComment);
            content.AppendLine(EXEC_FUNCTION_BLOCK);

            content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

            foreach (var repo in repos)
            {
                content.AppendLine(Exec(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, azureStorageConnectionString, noSslVerify, true, skipReleases, archiveRepos)));
            }

            return content.ToString();
        }

        private string GenerateParallelGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool skipReleases, bool archiveRepos)
        {
            if (!repos.Any())
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(PWSH_SHEBANG);
            content.AppendLine();
            content.AppendLine(VersionComment);
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
                content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, azureStorageConnectionString, noSslVerify, false, skipReleases, archiveRepos))}");
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

        private string GenerateSequentialAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoServerUrl, string adoSourceOrg, string githubTargetOrg)
        {
            if (!repos.Any())
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(PWSH_SHEBANG);
            content.AppendLine();
            content.AppendLine(VersionComment);
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
                        content.AppendLine(Exec(MigrateAdoRepoScript(adoServerUrl, adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, true)));
                    }
                }
            }

            return content.ToString();
        }

        private string GenerateParallelAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoServerUrl, string adoSourceOrg, string githubTargetOrg)
        {
            if (!repos.Any())
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(PWSH_SHEBANG);
            content.AppendLine();
            content.AppendLine(VersionComment);
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
                    content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateAdoRepoScript(adoServerUrl, adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, false))}");
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

        private string MigrateGithubRepoScript(string githubSourceOrg, string githubTargetOrg, string repo, string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify, bool wait, bool skipReleases, bool archiveRepo)
        {
            var ghesRepoOptions = "";
            if (ghesApiUrl.HasValue())
            {
                ghesRepoOptions = GetGhesRepoOptions(ghesApiUrl, azureStorageConnectionString, noSslVerify);
            }

            return $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(!string.IsNullOrEmpty(ghesRepoOptions) ? $" {ghesRepoOptions}" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}{(skipReleases ? " --skip-releases" : string.Empty)}{(archiveRepo ? " --archive-gh-repo" : string.Empty)}";
        }

        private string MigrateAdoRepoScript(string adoServerUrl, string adoSourceOrg, string teamProject, string adoRepo, string githubTargetOrg, string githubRepo, bool wait)
        {
            return $"gh gei migrate-repo{(adoServerUrl.HasValue() ? $" --ado-server-url \"{adoServerUrl}\"" : string.Empty)} --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{teamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}";
        }

        private string GetGhesRepoOptions(string ghesApiUrl, string azureStorageConnectionString, bool noSslVerify)
        {
            return $"--ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\"{(noSslVerify ? " --no-ssl-verify" : string.Empty)}";
        }

        private string WaitForMigrationScript(string repoMigrationKey = null) => $"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

        private string Exec(string script) => Wrap(script, "Exec");

        private string ExecAndGetMigrationId(string script) => Wrap(script, "ExecAndGetMigrationID");

        private string Wrap(string script, string outerCommand = "") =>
            script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

        private string VersionComment => $"# =========== Created with CLI version {_versionProvider.GetCurrentVersion()} ===========";

        private const string PWSH_SHEBANG = "#!/usr/bin/pwsh";

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

    public class GenerateScriptCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string AdoServerUrl { get; set; }
        public string AdoSourceOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubTargetOrg { get; set; }
        public FileInfo Output { get; set; }
        public string GhesApiUrl { get; set; }
        public string AzureStorageConnectionString { get; set; }
        public bool NoSslVerify { get; set; }
        public bool SkipReleases { get; set; }
        public bool Ssh { get; set; }
        public bool Sequential { get; set; }
        public string GithubSourcePat { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
        public bool ArchiveGhRepos { get; set; }
    }
}
