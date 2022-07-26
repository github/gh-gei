using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class GenerateScriptCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;
        private GenerateScriptOptions _generateScriptOptions;
        private readonly IVersionProvider _versionProvider;
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;

        private AdoInspectorService _adoInspectorService;

        public GenerateScriptCommand(OctoLogger log, AdoApiFactory adoApiFactory, IVersionProvider versionProvider, AdoInspectorServiceFactory adoInspectorServiceFactory) : base("generate-script")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
            _versionProvider = versionProvider;
            _adoInspectorServiceFactory = adoInspectorServiceFactory;

            Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT env variable or --ado-pat option to be set.";

            var githubOrgOption = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var adoOrgOption = new Option<string>("--ado-org")
            {
                IsRequired = false
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = false
            };
            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1"))
            {
                IsRequired = false
            };
            var sshOption = new Option("--ssh")
            {
                IsRequired = false,
                IsHidden = true
            };
            var sequential = new Option("--sequential")
            {
                IsRequired = false,
                Description = "Waits for each migration to finish before moving on to the next one."
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };
            var downloadMigrationLogs = new Option("--download-migration-logs")
            {
                IsRequired = false,
                Description = "Downloads the migration log for each repostiory migration."
            };

            var createTeams = new Option("--create-teams")
            {
                IsRequired = false,
                Description = "Includes create-team scripts that creates admins and maintainers teams and adds them to repos."
            };
            var linkIdpGroups = new Option("--link-idp-groups")
            {
                IsRequired = false,
                Description = "Adds --idp-group to the end of create teams scripts that links the created team to an idP group."
            };
            var lockAdoRepos = new Option("--lock-ado-repos")
            {
                IsRequired = false,
                Description = "Includes lock-ado-repo scripts that lock repos before migrating them."
            };
            var disableAdoRepos = new Option("--disable-ado-repos")
            {
                IsRequired = false,
                Description = "Includes disable-ado-repo scripts that disable repos after migrating them."
            };
            var integrateBoards = new Option("--integrate-boards")
            {
                IsRequired = false,
                Description = "Includes configure-autolink and integrate-boards scripts that configure Azure Boards integrations."
            };
            var rewirePipelines = new Option("--rewire-pipelines")
            {
                IsRequired = false,
                Description = "Includes share-service-connection and rewire-pipeline scripts that rewire Azure Pipelines to point to GitHub repos."
            };
            var all = new Option("--all")
            {
                IsRequired = false,
                Description = "Includes all script generation options."
            };
            var repoList = new Option<FileInfo>("--repo-list")
            {
                IsRequired = false,
                Description = "Path to a csv file that contains a list of repos to generate a script for. The CSV file should be generated using the inventory-report command."
            };

            AddOption(githubOrgOption);
            AddOption(adoOrgOption);
            AddOption(adoTeamProject);
            AddOption(outputOption);
            AddOption(sshOption);
            AddOption(sequential);
            AddOption(adoPat);
            AddOption(verbose);
            AddOption(downloadMigrationLogs);
            AddOption(createTeams);
            AddOption(linkIdpGroups);
            AddOption(lockAdoRepos);
            AddOption(disableAdoRepos);
            AddOption(integrateBoards);
            AddOption(rewirePipelines);
            AddOption(all);
            AddOption(repoList);

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

            LogOptions(args);

            _generateScriptOptions = new GenerateScriptOptions
            {
                CreateTeams = args.All || args.CreateTeams || args.LinkIdpGroups,
                LinkIdpGroups = args.All || args.LinkIdpGroups,
                LockAdoRepos = args.All || args.LockAdoRepos,
                DisableAdoRepos = args.All || args.DisableAdoRepos,
                IntegrateBoards = args.All || args.IntegrateBoards,
                RewirePipelines = args.All || args.RewirePipelines,
                DownloadMigrationLogs = args.All || args.DownloadMigrationLogs
            };

            var ado = _adoApiFactory.Create(args.AdoPat);
            _adoInspectorService = _adoInspectorServiceFactory.Create(ado);
            _adoInspectorService.OrgFilter = args.AdoOrg;
            _adoInspectorService.TeamProjectFilter = args.AdoTeamProject;

            if (args.RepoList.HasValue())
            {
                _log.LogInformation($"Loading Repo CSV File...");
                _adoInspectorService.LoadReposCsv(args.RepoList.FullName);
            }

            if (await _adoInspectorService.GetRepoCount() == 0)
            {
                _log.LogError("A migration script could not be generated because no migratable repos were found. Please note that the GEI does not migrate disabled or TFVC repos.");
                return;
            }

            var appIds = _generateScriptOptions.RewirePipelines ? await GetAppIds(ado, args.GithubOrg) : new Dictionary<string, string>();

            var script = args.Sequential
                ? await GenerateSequentialScript(appIds, args.GithubOrg)
                : await GenerateParallelScript(appIds, args.GithubOrg);

            _adoInspectorService.OutputRepoListToLog();

            await CheckForDuplicateRepoNames();

            if (args.Output.HasValue())
            {
                await WriteToFile(args.Output.FullName, script);
            }
        }

        private async Task<IDictionary<string, string>> GetAppIds(AdoApi ado, string githubOrg)
        {
            var appIds = new Dictionary<string, string>();

            foreach (var org in await _adoInspectorService.GetOrgs())
            {
                // Not using AdoInspector here because we want all team projects here, even if we are filtering to a specific one
                var appId = await ado.GetGithubAppId(org, githubOrg, await ado.GetTeamProjects(org));

                if (appId.HasValue())
                {
                    appIds.Add(org, appId);
                }
                else
                {
                    _log.LogWarning($"CANNOT FIND GITHUB APP SERVICE CONNECTION IN ADO ORGANIZATION: {org}. You must install the Pipelines app in GitHub and connect it to any Team Project in this ADO Org first.");
                }
            }

            return appIds;
        }

        private async Task CheckForDuplicateRepoNames()
        {
            var seen = new HashSet<string>();

            foreach (var org in await _adoInspectorService.GetOrgs())
            {
                foreach (var teamProject in await _adoInspectorService.GetTeamProjects(org))
                {
                    foreach (var repo in await _adoInspectorService.GetRepos(org, teamProject))
                    {
                        if (!seen.Add(GetGithubRepoName(teamProject, repo.Name)))
                        {
                            _log.LogWarning($"DUPLICATE REPO NAME: {GetGithubRepoName(teamProject, repo.Name)}");
                        }
                    }
                }
            }
        }

        private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo}".ReplaceInvalidCharactersWithDash();

        private string GetRepoMigrationKey(string adoOrg, string githubRepoName) => $"{adoOrg}/{githubRepoName}";

        private async Task<string> GenerateSequentialScript(IDictionary<string, string> appIds, string githubOrg)
        {
            var content = new StringBuilder();

            AppendLine(content, PWSH_SHEBANG);
            AppendLine(content);
            AppendLine(content, VersionComment);
            AppendLine(content, EXEC_FUNCTION_BLOCK);

            foreach (var adoOrg in await _adoInspectorService.GetOrgs())
            {
                AppendLine(content, $"# =========== Organization: {adoOrg} ===========");

                appIds.TryGetValue(adoOrg, out var appId);

                if (_generateScriptOptions.RewirePipelines && appId is null)
                {
                    AppendLine(content, "# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
                }

                foreach (var adoTeamProject in await _adoInspectorService.GetTeamProjects(adoOrg))
                {
                    AppendLine(content);
                    AppendLine(content, $"# === Team Project: {adoOrg}/{adoTeamProject} ===");

                    if (!(await _adoInspectorService.GetRepos(adoOrg, adoTeamProject)).Any())
                    {
                        AppendLine(content, "# Skipping this Team Project because it has no git repos");
                        continue;
                    }

                    AppendLine(content, Exec(CreateGithubMaintainersTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups)));
                    AppendLine(content, Exec(CreateGithubAdminsTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups)));
                    AppendLine(content, Exec(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId)));

                    foreach (var adoRepo in await _adoInspectorService.GetRepos(adoOrg, adoTeamProject))
                    {
                        var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo.Name);

                        AppendLine(content);
                        AppendLine(content, Exec(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                        AppendLine(content, Exec(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo.Name, githubOrg, githubRepo, true)));
                        AppendLine(content, Exec(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                        AppendLine(content, Exec(ConfigureAutolinkScript(githubOrg, githubRepo, adoOrg, adoTeamProject)));
                        AppendLine(content, Exec(AddMaintainersToGithubRepoScript(adoTeamProject, githubOrg, githubRepo)));
                        AppendLine(content, Exec(AddAdminsToGithubRepoScript(adoTeamProject, githubOrg, githubRepo)));
                        AppendLine(content, Exec(BoardsIntegrationScript(adoOrg, adoTeamProject, githubOrg, githubRepo)));
                        AppendLine(content, Exec(DownloadMigrationLogScript(githubOrg, githubRepo)));

                        foreach (var adoPipeline in await _adoInspectorService.GetPipelines(adoOrg, adoTeamProject, adoRepo.Name))
                        {
                            AppendLine(content, Exec(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId)));
                        }
                    }
                }

                AppendLine(content);
                AppendLine(content);
            }

            return content.ToString();
        }

        private async Task<string> GenerateParallelScript(IDictionary<string, string> appIds, string githubOrg)
        {
            var content = new StringBuilder();
            AppendLine(content, PWSH_SHEBANG);
            AppendLine(content);
            AppendLine(content, VersionComment);
            AppendLine(content, EXEC_FUNCTION_BLOCK);
            AppendLine(content, EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);
            AppendLine(content, EXEC_BATCH_FUNCTION_BLOCK);

            AppendLine(content);
            AppendLine(content, "$Succeeded = 0");
            AppendLine(content, "$Failed = 0");
            AppendLine(content, "$RepoMigrations = [ordered]@{}");

            // Queueing migrations
            foreach (var adoOrg in await _adoInspectorService.GetOrgs())
            {
                AppendLine(content);
                AppendLine(content, $"# =========== Queueing migration for Organization: {adoOrg} ===========");

                appIds.TryGetValue(adoOrg, out var appId);

                if (_generateScriptOptions.RewirePipelines && appId is null)
                {
                    AppendLine(content);
                    AppendLine(content, "# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
                }

                foreach (var adoTeamProject in await _adoInspectorService.GetTeamProjects(adoOrg))
                {
                    AppendLine(content);
                    AppendLine(content, $"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");

                    if (!(await _adoInspectorService.GetRepos(adoOrg, adoTeamProject)).Any())
                    {
                        AppendLine(content, "# Skipping this Team Project because it has no git repos");
                        continue;
                    }

                    AppendLine(content, Exec(CreateGithubMaintainersTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups)));
                    AppendLine(content, Exec(CreateGithubAdminsTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups)));
                    AppendLine(content, Exec(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId)));

                    // queue up repo migration for each ADO repo
                    foreach (var adoRepo in await _adoInspectorService.GetRepos(adoOrg, adoTeamProject))
                    {

                        var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo.Name);

                        AppendLine(content);
                        AppendLine(content, Exec(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                        AppendLine(content, QueueMigrateRepoScript(adoOrg, adoTeamProject, adoRepo.Name, githubOrg, githubRepo));
                        AppendLine(content, $"$RepoMigrations[\"{GetRepoMigrationKey(adoOrg, githubRepo)}\"] = $MigrationID");
                    }
                }
            }

            // Waiting for migrations
            foreach (var adoOrg in await _adoInspectorService.GetOrgs())
            {
                AppendLine(content);
                AppendLine(content, $"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");

                foreach (var adoTeamProject in await _adoInspectorService.GetTeamProjects(adoOrg))
                {
                    foreach (var adoRepo in await _adoInspectorService.GetRepos(adoOrg, adoTeamProject))
                    {
                        AppendLine(content);
                        AppendLine(content, $"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {adoRepo.Name}. Will then complete the below post migration steps. ===");

                        var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo.Name);
                        var repoMigrationKey = GetRepoMigrationKey(adoOrg, githubRepo);

                        AppendLine(content, "$CanExecuteBatch = $true");
                        AppendLine(content, $"if ($null -ne $RepoMigrations[\"{repoMigrationKey}\"]) {{");
                        AppendLine(content, "    " + WaitForMigrationScript(repoMigrationKey));
                        AppendLine(content, "    $CanExecuteBatch = ($lastexitcode -eq 0)");

                        AppendLine(content, "}");
                        AppendLine(content, "if ($CanExecuteBatch) {");
                        if (
                            _generateScriptOptions.CreateTeams ||
                            _generateScriptOptions.DisableAdoRepos ||
                            _generateScriptOptions.IntegrateBoards ||
                            _generateScriptOptions.RewirePipelines ||
                            _generateScriptOptions.DownloadMigrationLogs
                        )
                        {
                            AppendLine(content, "    ExecBatch @(");
                            AppendLine(content, "        " + Wrap(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                            AppendLine(content, "        " + Wrap(ConfigureAutolinkScript(githubOrg, githubRepo, adoOrg, adoTeamProject)));
                            AppendLine(content, "        " + Wrap(AddMaintainersToGithubRepoScript(adoTeamProject, githubOrg, githubRepo)));
                            AppendLine(content, "        " + Wrap(AddAdminsToGithubRepoScript(adoTeamProject, githubOrg, githubRepo)));
                            AppendLine(content, "        " + Wrap(BoardsIntegrationScript(adoOrg, adoTeamProject, githubOrg, githubRepo)));
                            AppendLine(content, "        " + Wrap(DownloadMigrationLogScript(githubOrg, githubRepo)));

                            appIds.TryGetValue(adoOrg, out var appId);
                            foreach (var adoPipeline in await _adoInspectorService.GetPipelines(adoOrg, adoTeamProject, adoRepo.Name))
                            {
                                AppendLine(content, "        " + Wrap(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId)));
                            }

                            AppendLine(content, "    )");
                            AppendLine(content, "    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
                        }
                        else
                        {
                            AppendLine(content, "    $Succeeded++");
                        }

                        AppendLine(content, "} else {"); // if ($lastexitcode -ne 0)
                        AppendLine(content, "    $Failed++");
                        AppendLine(content, "}");
                    }
                }
            }

            // Generating report
            AppendLine(content);
            AppendLine(content, "Write-Host =============== Summary ===============");
            AppendLine(content, "Write-Host Total number of successful migrations: $Succeeded");
            AppendLine(content, "Write-Host Total number of failed migrations: $Failed");

            AppendLine(content, @"
if ($Failed -ne 0) {
    exit 1
}");

            AppendLine(content);
            AppendLine(content);

            return content.ToString();
        }

        private void AppendLine(StringBuilder sb, string content)
        {
            if (content.IsNullOrWhiteSpace())
            {
                return;
            }

            sb.AppendLine(content);
        }

        private void AppendLine(StringBuilder sb) => sb.AppendLine();

        private string DisableAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo) =>
            _generateScriptOptions.DisableAdoRepos
                ? $"./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string LockAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo) =>
            _generateScriptOptions.LockAdoRepos
                ? $"./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string ShareServiceConnectionScript(string adoOrg, string adoTeamProject, string appId) =>
            _generateScriptOptions.RewirePipelines && appId.HasValue()
                ? $"./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string ConfigureAutolinkScript(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject) =>
            _generateScriptOptions.IntegrateBoards
                ? $"./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string MigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool wait) =>
            $"./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}";

        private string QueueMigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo) =>
            $"$MigrationID = {ExecAndGetMigrationId(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, false))}";

        private string CreateGithubMaintainersTeamScript(string adoTeamProject, string githubOrg, bool linkIdpGroups) =>
            _generateScriptOptions.CreateTeams
                ? $"./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Maintainers\"{(_log.Verbose ? " --verbose" : string.Empty)}{(linkIdpGroups ? $" --idp-group \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Maintainers\"" : string.Empty)}"
                : null;

        private string CreateGithubAdminsTeamScript(string adoTeamProject, string githubOrg, bool linkIdpGroups) =>
            _generateScriptOptions.CreateTeams
                ? $"./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Admins\"{(_log.Verbose ? " --verbose" : string.Empty)}{(linkIdpGroups ? $" --idp-group \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Admins\"" : string.Empty)}"
                : null;

        private string AddMaintainersToGithubRepoScript(string adoTeamProject, string githubOrg, string githubRepo) =>
            _generateScriptOptions.CreateTeams
                ? $"./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Maintainers\" --role \"maintain\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string AddAdminsToGithubRepoScript(string adoTeamProject, string githubOrg, string githubRepo) =>
            _generateScriptOptions.CreateTeams
                ? $"./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Admins\" --role \"admin\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string RewireAzurePipelineScript(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string appId) =>
            _generateScriptOptions.RewirePipelines && appId.HasValue()
                ? $"./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{adoPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string BoardsIntegrationScript(string adoOrg, string adoTeamProject, string githubOrg, string githubRepo) =>
            _generateScriptOptions.IntegrateBoards
                ? $"./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
                : null;

        private string WaitForMigrationScript(string repoMigrationKey) => $"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

        private string DownloadMigrationLogScript(string githubOrg, string githubRepo) =>
            _generateScriptOptions.DownloadMigrationLogs
            ? $"./ado2gh download-logs --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\""
            : null;

        private string Exec(string script) => Wrap(script, "Exec");

        private string ExecAndGetMigrationId(string script) => Wrap(script, "ExecAndGetMigrationID");

        private string Wrap(string script, string outerCommand = "") =>
            script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

        private void LogOptions(GenerateScriptCommandArgs args)
        {
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            if (args.AdoOrg.HasValue())
            {
                _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            }
            if (args.AdoTeamProject.HasValue())
            {
                _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            }
            if (args.DownloadMigrationLogs)
            {
                _log.LogInformation("DOWNLOAD MIGRATION LOGS: true");
            }
            if (args.Output.HasValue())
            {
                _log.LogInformation($"OUTPUT: {args.Output}");
            }
            if (args.Ssh)
            {
                _log.LogWarning("SSH mode is no longer supported. --ssh flag will be ignored.");
            }
            if (args.Sequential)
            {
                _log.LogInformation("SEQUENTIAL: true");
            }
            if (args.AdoPat.HasValue())
            {
                _log.LogInformation("ADO PAT: ***");
            }
            if (args.CreateTeams)
            {
                _log.LogInformation("CREATE TEAMS: true");
            }
            if (args.LinkIdpGroups)
            {
                _log.LogInformation("LINK IDP GROUPS: true");
            }
            if (args.LockAdoRepos)
            {
                _log.LogInformation("LOCK ADO REPOS: true");
            }
            if (args.DisableAdoRepos)
            {
                _log.LogInformation("DISABLE ADO REPOS: true");
            }
            if (args.IntegrateBoards)
            {
                _log.LogInformation("INTEGRATE BOARDS: true");
            }
            if (args.RewirePipelines)
            {
                _log.LogInformation("REWRITE PIPELINES: true");
            }
            if (args.All)
            {
                _log.LogInformation("ALL: true");
            }
            if (args.RepoList.HasValue())
            {
                _log.LogInformation($"REPO LIST: {args.RepoList}");
            }
        }

        private class GenerateScriptOptions
        {
            public bool CreateTeams { get; init; }
            public bool LinkIdpGroups { get; init; }
            public bool LockAdoRepos { get; init; }
            public bool DisableAdoRepos { get; init; }
            public bool IntegrateBoards { get; init; }
            public bool RewirePipelines { get; init; }
            public bool DownloadMigrationLogs { get; init; }
        }

        private string VersionComment => $"# =========== Created with CLI version {_versionProvider.GetCurrentVersion()} ===========";

        private const string PWSH_SHEBANG = "#!/usr/bin/env pwsh";

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

        private const string EXEC_BATCH_FUNCTION_BLOCK = @"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}";
    }

    public class GenerateScriptCommandArgs
    {
        public string GithubOrg { get; set; }
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public FileInfo Output { get; set; }
        public bool Ssh { get; set; }
        public bool Sequential { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool CreateTeams { get; set; }
        public bool LinkIdpGroups { get; set; }
        public bool LockAdoRepos { get; set; }
        public bool DisableAdoRepos { get; set; }
        public bool IntegrateBoards { get; set; }
        public bool RewirePipelines { get; set; }
        public bool All { get; set; }
        public FileInfo RepoList { get; set; }
    }
}
