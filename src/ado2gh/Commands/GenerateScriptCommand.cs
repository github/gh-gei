using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class GenerateScriptCommand : Command
    {
        private bool _reposOnly;
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public GenerateScriptCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base("generate-script")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

            Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT env variable to be set.";

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
            var reposOnlyOption = new Option("--repos-only")
            {
                IsRequired = false
            };
            var skipIdpOption = new Option("--skip-idp")
            {
                IsRequired = false
            };
            var sshOption = new Option("--ssh")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrgOption);
            AddOption(adoOrgOption);
            AddOption(adoTeamProject);
            AddOption(outputOption);
            AddOption(reposOnlyOption);
            AddOption(skipIdpOption);
            AddOption(sshOption);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, FileInfo, bool, bool, bool, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string adoOrg, string adoTeamProject, FileInfo output, bool reposOnly, bool skipIdp, bool ssh = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Script...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"OUTPUT: {output}");
            if (ssh)
            {
                _log.LogInformation("SSH: true");
            }

            _reposOnly = reposOnly;

            var ado = _adoApiFactory.Create();

            var orgs = await GetOrgs(ado, adoOrg);
            var repos = await GetRepos(ado, orgs, adoTeamProject);
            var pipelines = _reposOnly ? null : await GetPipelines(ado, repos);
            var appIds = _reposOnly ? null : await GetAppIds(ado, orgs, adoTeamProject, githubOrg);

            CheckForDuplicateRepoNames(repos);

            var script = GenerateScript(repos, pipelines, appIds, githubOrg, skipIdp, ssh);

            if (output != null)
            {
                File.WriteAllText(output.FullName, script);
            }
        }

        public async Task<IDictionary<string, string>> GetAppIds(AdoApi ado, IEnumerable<string> orgs, string adoTeamProject, string githubOrg)
        {
            var appIds = new Dictionary<string, string>();

            if (orgs != null && ado != null)
            {
                foreach (var org in orgs)
                {
                    var teamProjects = await ado.GetTeamProjects(org);
                    var appId = await ado.GetGithubAppId(org, githubOrg, teamProjects, adoTeamProject);

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
                foreach (var org in orgs)
                {
                    _log.LogInformation($"ADO ORG: {org}");
                    repos.Add(org, new Dictionary<string, IEnumerable<string>>());

                    var teamProjects = await ado.GetTeamProjects(org);
                    if (string.IsNullOrEmpty(adoTeamProject))
                    {
                        foreach (var teamProject in teamProjects)
                        {
                            await GetTeamProjectRepos(ado, repos, org, teamProject);
                        }
                    }
                    else
                    {
                        if (teamProjects.Any(o => o.Equals(adoTeamProject, StringComparison.OrdinalIgnoreCase)))
                        {
                            await GetTeamProjectRepos(ado, repos, org, adoTeamProject);
                        }
                    }
                }
            }

            return repos;
        }

        private async Task GetTeamProjectRepos(AdoApi ado, Dictionary<string, IDictionary<string, IEnumerable<string>>> repos, string org, string teamProject)
        {
            _log.LogInformation($"  Team Project: {teamProject}");
            var projectRepos = await ado.GetRepos(org, teamProject);
            repos[org].Add(teamProject, projectRepos);

            foreach (var repo in projectRepos)
            {
                _log.LogInformation($"    Repo: {repo}");
            }
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

        public string GenerateScript(IDictionary<string, IDictionary<string, IEnumerable<string>>> repos,
                                          IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines,
                                          IDictionary<string, string> appIds,
                                          string githubOrg,
                                          bool skipIdp,
                                          bool ssh)
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

            foreach (var adoOrg in repos.Keys)
            {
                content.AppendLine($"# =========== Organization: {adoOrg} ===========");

                var appId = string.Empty;
                var hasAppId = appIds != null && appIds.TryGetValue(adoOrg, out appId);

                if (!hasAppId && !_reposOnly)
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
                    }
                    else
                    {
                        content.AppendLine(CreateGithubTeamsScript(adoTeamProject, githubOrg, skipIdp));

                        if (hasAppId)
                        {
                            content.AppendLine(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId));
                        }

                        foreach (var adoRepo in repos[adoOrg][adoTeamProject])
                        {
                            content.AppendLine();

                            var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo);

                            content.AppendLine(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo));
                            content.AppendLine(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, ssh));
                            content.AppendLine(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo));
                            content.AppendLine(AutolinkScript(githubOrg, githubRepo, adoOrg, adoTeamProject));
                            content.AppendLine(GithubRepoPermissionsScript(adoTeamProject, githubOrg, githubRepo));
                            content.AppendLine(BoardsIntegrationScript(adoOrg, adoTeamProject, githubOrg, githubRepo));

                            if (hasAppId && pipelines != null)
                            {
                                foreach (var adoPipeline in pipelines[adoOrg][adoTeamProject][adoRepo])
                                {
                                    content.AppendLine(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId));
                                }
                            }
                        }
                    }
                }

                content.AppendLine();
                content.AppendLine();
            }

            return content.ToString();
        }

        private string DisableAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo)
        {
            return _reposOnly
                ? string.Empty
                : $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string LockAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo)
        {
            return _reposOnly
                ? string.Empty
                : $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string ShareServiceConnectionScript(string adoOrg, string adoTeamProject, string appId)
        {
            return _reposOnly
                ? string.Empty
                : $"Exec {{ ./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string AutolinkScript(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject)
        {
            return _reposOnly
                ? string.Empty
                : $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string MigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool ssh)
        {
            return $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(ssh ? " --ssh" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string CreateGithubTeamsScript(string adoTeamProject, string githubOrg, bool skipIdp)
        {
            if (_reposOnly)
            {
                return string.Empty;
            }

            var result = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\"{(_log.Verbose ? " --verbose" : string.Empty)}";

            if (!skipIdp)
            {
                result += $" --idp-group \"{adoTeamProject}-Maintainers\"";
            }

            result += " }";
            result += Environment.NewLine;

            result += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\"{(_log.Verbose ? " --verbose" : string.Empty)}";

            if (!skipIdp)
            {
                result += $" --idp-group \"{adoTeamProject}-Admins\"";
            }

            result += " }";

            return result;
        }

        private string GithubRepoPermissionsScript(string adoTeamProject, string githubOrg, string githubRepo)
        {
            if (_reposOnly)
            {
                return string.Empty;
            }

            var result = $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
            result += Environment.NewLine;
            result += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";

            return result;
        }

        private string RewireAzurePipelineScript(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string appId)
        {
            return _reposOnly
                ? string.Empty
                : $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{adoPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }

        private string BoardsIntegrationScript(string adoOrg, string adoTeamProject, string githubOrg, string githubRepo)
        {
            return _reposOnly
                ? string.Empty
                : $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)} }}";
        }
    }
}
