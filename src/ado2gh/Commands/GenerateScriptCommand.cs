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
namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class GenerateScriptCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = (path, contents) => File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public GenerateScriptCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base("generate-script")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

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
            var createTeams = new Option("--create-teams")
            {
                IsRequired = false
            };
            var linkIdpGroups = new Option("--link-idp-groups")
            {
                IsRequired = false
            };
            var lockAdoRepos = new Option("--lock-ado-repos")
            {
                IsRequired = false
            };
            var disableAdoRepos = new Option("--disable-ado-repos")
            {
                IsRequired = false
            };
            var addTeamsToRepos = new Option("--add-teams-to-repos")
            {
                IsRequired = false
            };
            var integrateBoards = new Option("--integrate-boards")
            {
                IsRequired = false
            };
            var rewirePipelines = new Option("--rewire-pipelines")
            {
                IsRequired = false
            };
            var all = new Option("--all")
            {
                IsRequired = false
            };

            AddOption(githubOrgOption);
            AddOption(adoOrgOption);
            AddOption(adoTeamProject);
            AddOption(outputOption);
            AddOption(sshOption);
            AddOption(sequential);
            AddOption(adoPat);
            AddOption(verbose);
            AddOption(createTeams);
            AddOption(linkIdpGroups);
            AddOption(lockAdoRepos);
            AddOption(disableAdoRepos);
            AddOption(addTeamsToRepos);
            AddOption(integrateBoards);
            AddOption(rewirePipelines);
            AddOption(all);

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

            var ado = _adoApiFactory.Create(args.AdoPat);

            var orgs = await GetOrgs(ado, args.AdoOrg);
            var repos = await GetRepos(ado, orgs, args.AdoTeamProject);
            var pipelines = args.All || args.RewirePipelines ? await GetPipelines(ado, repos) : null;
            var appIds = args.All || args.RewirePipelines ? await GetAppIds(ado, orgs, args.GithubOrg) : null;

            CheckForDuplicateRepoNames(repos);

            var generateScriptOptions = new GenerateScriptOptions
            {
                CreateTeams = args.All || args.CreateTeams || args.LinkIdpGroups,
                LinkIdpGroups = args.All || args.LinkIdpGroups,
                LockAdoRepos = args.All || args.LockAdoRepos,
                DisableAdoRepos = args.All || args.DisableAdoRepos,
                AddTeamsToRepos = args.All || args.AddTeamsToRepos,
                IntegrateBoards = args.All || args.IntegrateBoards,
                RewirePipelines = args.All || args.RewirePipelines
            };

            var script = args.Sequential
                ? GenerateSequentialScript(repos, pipelines, appIds, args.GithubOrg, generateScriptOptions)
                : GenerateParallelScript(repos, pipelines, appIds, args.GithubOrg, generateScriptOptions);

            if (args.Output.HasValue())
            {
                await WriteToFile(args.Output.FullName, script);
            }
        }

        public async Task<IDictionary<string, string>> GetAppIds(AdoApi ado, IEnumerable<string> orgs, string githubOrg)
        {
            var appIds = new Dictionary<string, string>();

            if (orgs != null && ado != null)
            {
                foreach (var org in orgs)
                {
                    var teamProjects = await ado.GetTeamProjects(org);
                    var appId = await ado.GetGithubAppId(org, githubOrg, teamProjects);

                    if (string.IsNullOrWhiteSpace(appId))
                    {
                        _log.LogWarning($"CANNOT FIND GITHUB APP SERVICE CONNECTION IN ADO ORGANIZATION: {org}. You must install the Pipelines app in GitHub and connect it to any Team Project in this ADO Org first.");
                    }
                    else
                    {
                        appIds.Add(org, appId);
                    }
                }
            }

            return appIds;
        }

        public async Task<IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>> GetPipelines(AdoApi ado, IDictionary<string, IDictionary<string, IEnumerable<string>>> repos)
        {
            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>();

            if (repos != null && ado != null)
            {
                foreach (var org in repos.Keys)
                {
                    pipelines.Add(org, new Dictionary<string, IDictionary<string, IEnumerable<string>>>());

                    foreach (var teamProject in repos[org].Keys)
                    {
                        pipelines[org].Add(teamProject, new Dictionary<string, IEnumerable<string>>());

                        foreach (var repo in repos[org][teamProject])
                        {
                            var repoId = await ado.GetRepoId(org, teamProject, repo);
                            var repoPipelines = await ado.GetPipelines(org, teamProject, repoId);

                            pipelines[org][teamProject].Add(repo, repoPipelines);
                        }
                    }
                }
            }

            return pipelines;
        }

        public async Task<IDictionary<string, IDictionary<string, IEnumerable<string>>>> GetRepos(AdoApi ado, IEnumerable<string> orgs, string adoTeamProject)
        {
            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();

            if (orgs != null && ado != null)
            {
                var teamProjectExists = false;
                foreach (var org in orgs)
                {
                    _log.LogInformation($"ADO ORG: {org}");
                    repos.Add(org, new Dictionary<string, IEnumerable<string>>());

                    var teamProjects = await ado.GetTeamProjects(org);
                    if (string.IsNullOrEmpty(adoTeamProject))
                    {
                        foreach (var teamProject in teamProjects)
                        {
                            teamProjectExists = true;
                            var projectRepos = await GetTeamProjectRepos(ado, org, teamProject);
                            repos[org].Add(teamProject, projectRepos);
                        }
                    }
                    else
                    {
                        if (teamProjects.Any(o => o.Equals(adoTeamProject, StringComparison.OrdinalIgnoreCase)))
                        {
                            teamProjectExists = true;
                            var projectRepos = await GetTeamProjectRepos(ado, org, adoTeamProject);
                            repos[org].Add(adoTeamProject, projectRepos);
                        }
                    }
                }
                if (!teamProjectExists)
                {
                    _log.LogWarning($"ADO Team Project provided cannot be found [{adoTeamProject}]");
                }
            }

            return repos;
        }

        private async Task<IEnumerable<string>> GetTeamProjectRepos(AdoApi ado, string org, string teamProject)
        {
            _log.LogInformation($"  Team Project: {teamProject}");
            var projectRepos = await ado.GetEnabledRepos(org, teamProject);

            foreach (var repo in projectRepos)
            {
                _log.LogInformation($"    Repo: {repo}");
            }
            return projectRepos;
        }

        public async Task<IEnumerable<string>> GetOrgs(AdoApi ado, string adoOrg)
        {
            var orgs = new List<string>();

            if (!string.IsNullOrWhiteSpace(adoOrg))
            {
                _log.LogInformation($"ADO Org provided, only processing repos for {adoOrg}");
                orgs.Add(adoOrg);
            }
            else
            {
                if (ado != null)
                {
                    _log.LogInformation($"No ADO Org provided, retrieving list of all Orgs PAT has access to...");
                    // TODO: Check if the PAT has the proper permissions to retrieve list of ADO orgs, needs the All Orgs scope
                    var userId = await ado.GetUserId();
                    orgs = (await ado.GetOrganizations(userId)).ToList();
                }
            }

            return orgs;
        }

        private void CheckForDuplicateRepoNames(IDictionary<string, IDictionary<string, IEnumerable<string>>> repos)
        {
            var duplicateRepoNames = repos.SelectMany(x => x.Value)
                                          .SelectMany(x => x.Value.Select(y => GetGithubRepoName(x.Key, y)))
                                          .GroupBy(x => x)
                                          .Where(x => x.Count() > 1)
                                          .Select(x => x.Key)
                                          .ToList();

            foreach (var duplicate in duplicateRepoNames)
            {
                _log.LogWarning($"DUPLICATE REPO NAME: {duplicate}");
            }
        }

        private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo.Replace(" ", "-")}";

        private string GetRepoMigrationKey(string adoOrg, string githubRepoName) => $"{adoOrg}/{githubRepoName}";

        private string GenerateSequentialScript(IDictionary<string, IDictionary<string, IEnumerable<string>>> repos,
            IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines,
            IDictionary<string, string> appIds,
            string githubOrg,
            GenerateScriptOptions options)
        {
            if (repos is null || !repos.Any())
            {
                return string.Empty;
            }

            var content = new StringBuilder();

            content.AppendLine(PWSH_SHEBANG);
            content.AppendLine(EXEC_FUNCTION_BLOCK);

            foreach (var adoOrg in repos.Keys)
            {
                content.AppendLine($"# =========== Organization: {adoOrg} ===========");

                var appId = string.Empty;
                var hasAppId = appIds != null && appIds.TryGetValue(adoOrg, out appId);

                if (options.RewirePipelines && !hasAppId)
                {
                    content.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
                }

                foreach (var adoTeamProject in repos[adoOrg].Keys)
                {
                    content.AppendLine();
                    content.AppendLine($"# === Team Project: {adoOrg}/{adoTeamProject} ===");

                    if (!repos[adoOrg][adoTeamProject].Any())
                    {
                        content.AppendLine("# Skipping this Team Project because it has no git repos");
                        continue;
                    }

                    if (options.CreateTeams)
                    {
                        content.AppendLine(CreateGithubTeamsScript(adoTeamProject, githubOrg, options.LinkIdpGroups));
                    }

                    if (options.RewirePipelines && hasAppId)
                    {
                        content.AppendLine(Exec(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId)));
                    }

                    foreach (var adoRepo in repos[adoOrg][adoTeamProject])
                    {
                        content.AppendLine();

                        var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo);

                        if (options.LockAdoRepos)
                        {
                            content.AppendLine(Exec(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo)));
                        }

                        content.AppendLine(Exec(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, true)));

                        if (options.DisableAdoRepos)
                        {
                            content.AppendLine(Exec(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo)));
                        }

                        if (options.IntegrateBoards)
                        {
                            content.AppendLine(Exec(AutolinkScript(githubOrg, githubRepo, adoOrg, adoTeamProject)));
                        }

                        if (options.AddTeamsToRepos)
                        {
                            content.AppendLine(GithubRepoPermissionsScript(adoTeamProject, githubOrg, githubRepo));
                        }

                        if (options.IntegrateBoards)
                        {
                            content.AppendLine(Exec(BoardsIntegrationScript(adoOrg, adoTeamProject, githubOrg, githubRepo)));
                        }

                        if (options.RewirePipelines && hasAppId && pipelines != null)
                        {
                            foreach (var adoPipeline in pipelines[adoOrg][adoTeamProject][adoRepo])
                            {
                                content.AppendLine(Exec(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId)));
                            }
                        }
                    }
                }

                content.AppendLine();
                content.AppendLine();
            }

            return content.ToString();
        }

        private string GenerateParallelScript(IDictionary<string, IDictionary<string, IEnumerable<string>>> repos,
            IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines,
            IDictionary<string, string> appIds,
            string githubOrg,
            GenerateScriptOptions options)
        {
            if (repos is null || !repos.Any())
            {
                return string.Empty;
            }

            var content = new StringBuilder();
            content.AppendLine(PWSH_SHEBANG);
            content.AppendLine(EXEC_FUNCTION_BLOCK);
            content.AppendLine(EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);
            content.AppendLine(EXEC_BATCH_FUNCTION_BLOCK);

            content.AppendLine();
            content.AppendLine("$Succeeded = 0");
            content.AppendLine("$Failed = 0");
            content.AppendLine("$RepoMigrations = [ordered]@{}");

            // Queueing migrations
            foreach (var adoOrg in repos.Keys)
            {
                content.AppendLine();
                content.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");

                var appId = string.Empty;
                var hasAppId = appIds != null && appIds.TryGetValue(adoOrg, out appId);

                if (options.RewirePipelines && !hasAppId)
                {
                    content.AppendLine();
                    content.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
                }

                foreach (var adoTeamProject in repos[adoOrg].Keys)
                {
                    content.AppendLine();
                    content.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");

                    if (!repos[adoOrg][adoTeamProject].Any())
                    {
                        content.AppendLine("# Skipping this Team Project because it has no git repos");
                        continue;
                    }

                    if (options.CreateTeams)
                    {
                        content.AppendLine(CreateGithubTeamsScript(adoTeamProject, githubOrg, options.LinkIdpGroups));
                    }

                    if (options.RewirePipelines && hasAppId)
                    {
                        content.AppendLine(Exec(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId)));
                    }

                    // queue up repo migration for each ADO repo
                    foreach (var adoRepo in repos[adoOrg][adoTeamProject])
                    {
                        content.AppendLine();

                        var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo);

                        if (options.LockAdoRepos)
                        {
                            content.AppendLine(Exec(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo)));
                        }

                        content.AppendLine(QueueMigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo));
                        content.AppendLine($"$RepoMigrations[\"{GetRepoMigrationKey(adoOrg, githubRepo)}\"] = $MigrationID");
                    }
                }
            }

            // Waiting for migrations
            foreach (var adoOrg in repos.Keys)
            {
                content.AppendLine();
                content.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");

                foreach (var adoTeamProject in repos[adoOrg].Keys)
                {
                    foreach (var adoRepo in repos[adoOrg][adoTeamProject])
                    {
                        content.AppendLine();
                        content.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {adoRepo}. Will then complete the below post migration steps. ===");

                        var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo);
                        var repoMigrationKey = GetRepoMigrationKey(adoOrg, githubRepo);

                        content.AppendLine(WaitForMigrationScript(repoMigrationKey));
                        content.AppendLine("if ($lastexitcode -eq 0) {");
                        if (options.Any())
                        {
                            content.AppendLine("    ExecBatch @(");
                            content.AppendLine("        " + Wrap(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo)));
                            content.AppendLine("        " + Wrap(AutolinkScript(githubOrg, githubRepo, adoOrg, adoTeamProject)));
                            content.AppendLine("        " + Wrap(GithubRepoMaintainPermissionScript(adoTeamProject, githubOrg, githubRepo)));
                            content.AppendLine("        " + Wrap(GithubRepoAdminPermissionScript(adoTeamProject, githubOrg, githubRepo)));
                            content.AppendLine("        " + Wrap(BoardsIntegrationScript(adoOrg, adoTeamProject, githubOrg, githubRepo)));

                            var appId = string.Empty;
                            var hasAppId = appIds != null && appIds.TryGetValue(adoOrg, out appId);
                            if (options.RewirePipelines && hasAppId && pipelines != null)
                            {
                                foreach (var adoPipeline in pipelines[adoOrg][adoTeamProject][adoRepo])
                                {
                                    content.AppendLine("        " + Wrap(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId)));
                                }
                            }

                            content.AppendLine("    )");
                            content.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
                        }
                        else
                        {
                            content.AppendLine("    $Succeeded++");
                        }

                        content.AppendLine("} else {"); // if ($lastexitcode -ne 0)
                        content.AppendLine("    $Failed++");
                        content.AppendLine("}");
                    }
                }
            }

            // Generating report
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

        private string DisableAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo) =>
            $"./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string LockAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo) =>
            $"./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string ShareServiceConnectionScript(string adoOrg, string adoTeamProject, string appId) =>
            $"./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string AutolinkScript(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject) =>
            $"./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string MigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool wait) =>
            $"./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}";

        private string QueueMigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo) =>
            $"$MigrationID = {ExecAndGetMigrationId(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, false))}";

        private string CreateGithubTeamsScript(string adoTeamProject, string githubOrg, bool linkIdpGroups)
        {
            var result = new StringBuilder();

            result.AppendLine(Exec(CreateGithubMaintainersTeamScript(adoTeamProject, githubOrg, linkIdpGroups)));
            result.Append(Exec(CreateGithubAdminsTeamScript(adoTeamProject, githubOrg, linkIdpGroups)));

            return result.ToString();
        }

        private string CreateGithubMaintainersTeamScript(string adoTeamProject, string githubOrg, bool linkIdpGroups) =>
            $"./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\"{(_log.Verbose ? " --verbose" : string.Empty)}{(linkIdpGroups ? $" --idp-group \"{adoTeamProject}-Maintainers\"" : string.Empty)}";

        private string CreateGithubAdminsTeamScript(string adoTeamProject, string githubOrg, bool linkIdpGroups) =>
            $"./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\"{(_log.Verbose ? " --verbose" : string.Empty)}{(linkIdpGroups ? $" --idp-group \"{adoTeamProject}-Admins\"" : string.Empty)}";

        private string GithubRepoPermissionsScript(string adoTeamProject, string githubOrg, string githubRepo)
        {
            var result = new StringBuilder();

            result.AppendLine(Exec(GithubRepoMaintainPermissionScript(adoTeamProject, githubOrg, githubRepo)));
            result.Append(Exec(GithubRepoAdminPermissionScript(adoTeamProject, githubOrg, githubRepo)));

            return result.ToString();
        }

        private string GithubRepoMaintainPermissionScript(string adoTeamProject, string githubOrg, string githubRepo) =>
            $"./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string GithubRepoAdminPermissionScript(string adoTeamProject, string githubOrg, string githubRepo) =>
            $"./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string RewireAzurePipelineScript(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string appId) =>
            $"./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{adoPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string BoardsIntegrationScript(string adoOrg, string adoTeamProject, string githubOrg, string githubRepo) =>
            $"./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}";

        private string WaitForMigrationScript(string repoMigrationKey) => $"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

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
            if (args.AddTeamsToRepos)
            {
                _log.LogInformation("ADD TEAMS TO REPOS: true");
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
        }

        private class GenerateScriptOptions
        {
            public bool CreateTeams { get; init; }
            public bool LinkIdpGroups { get; init; }
            public bool LockAdoRepos { get; init; }
            public bool DisableAdoRepos { get; init; }
            public bool AddTeamsToRepos { get; init; }
            public bool IntegrateBoards { get; init; }
            public bool RewirePipelines { get; init; }

            public bool Any() => CreateTeams
                               && LinkIdpGroups
                               && LockAdoRepos
                               && DisableAdoRepos
                               && AddTeamsToRepos
                               && IntegrateBoards
                               && RewirePipelines;
        }

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
        public bool CreateTeams { get; set; }
        public bool LinkIdpGroups { get; set; }
        public bool LockAdoRepos { get; set; }
        public bool DisableAdoRepos { get; set; }
        public bool AddTeamsToRepos { get; set; }
        public bool IntegrateBoards { get; set; }
        public bool RewirePipelines { get; set; }
        public bool All { get; set; }
    }
}
