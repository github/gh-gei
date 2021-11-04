using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class GenerateScriptCommand : Command
    {
        private bool _reposOnly;

        public GenerateScriptCommand() : base("generate-script")
        {
            var githubOrgOption = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var adoOrgOption = new Option<string>("--ado-org")
            {
                IsRequired = false
            };
            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./octoshift.sh"))
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

            AddOption(githubOrgOption);
            AddOption(adoOrgOption);
            AddOption(outputOption);
            AddOption(reposOnlyOption);
            AddOption(skipIdpOption);

            Handler = CommandHandler.Create<string, string, FileInfo, bool, bool>(Invoke);
        }

        private async Task Invoke(string githubOrg, string adoOrg, FileInfo output, bool reposOnly, bool skipIdp)
        {
            Console.WriteLine("Generating Script...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"OUTPUT: {output}");

            _reposOnly = reposOnly;

            using var ado = AdoApiFactory.Create();

            var orgs = new List<string>();
            var repos = new Dictionary<string, Dictionary<string, IEnumerable<string>>>();
            var pipelines = new Dictionary<string, Dictionary<string, Dictionary<string, IEnumerable<string>>>>();
            var appIds = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(adoOrg))
            {
                Console.WriteLine($"ADO Org provided, only processing repos for {adoOrg}");
                orgs.Add(adoOrg);
            }
            else
            {
                Console.WriteLine($"No ADO Org provided, retrieving list of all Orgs PAT has access to...");
                // TODO: Check if the PAT has the proper permissions to retrieve list of ADO orgs, needs the All Orgs scope
                var userId = await ado.GetUserId();
                orgs = await ado.GetOrganizations(userId);
            }

            foreach (var org in orgs)
            {
                Console.WriteLine($"ADO ORG: {org}");
                repos.Add(org, new Dictionary<string, IEnumerable<string>>());
                pipelines.Add(org, new Dictionary<string, Dictionary<string, IEnumerable<string>>>());

                var teamProjects = await ado.GetTeamProjects(org);

                foreach (var teamProject in teamProjects)
                {
                    Console.WriteLine($"  Team Project: {teamProject}");
                    var projectRepos = await ado.GetRepos(org, teamProject);
                    repos[org].Add(teamProject, projectRepos);

                    pipelines[org].Add(teamProject, new Dictionary<string, IEnumerable<string>>());

                    foreach (var repo in projectRepos)
                    {
                        Console.WriteLine($"    Repo: {repo}");
                        var repoId = await ado.GetRepoId(org, teamProject, repo);
                        var repoPipelines = await ado.GetPipelines(org, teamProject, repoId);

                        pipelines[org][teamProject].Add(repo, repoPipelines);
                    }
                }

                if (!_reposOnly)
                {
                    var appId = await ado.GetGithubAppId(org, githubOrg, teamProjects);

                    if (string.IsNullOrWhiteSpace(appId))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"WARNING: CANNOT FIND GITHUB APP SERVICE CONNECTION IN ADO ORGANIZATION: {org}. You must install the Pipelines app in GitHub and connect it to any Team Project in this ADO Org first.");
                        Console.ResetColor();
                    }
                    else
                    {
                        appIds.Add(org, appId);
                    }
                }
            }

            CheckForDuplicateRepoNames(repos);

            GenerateScript(repos, pipelines, appIds, githubOrg, output, skipIdp);
        }

        private void CheckForDuplicateRepoNames(Dictionary<string, Dictionary<string, IEnumerable<string>>> repos)
        {
            var duplicateRepoNames = repos.SelectMany(x => x.Value)
                                          .SelectMany(x => x.Value.Select(y => GetGithubRepoName(x.Key, y)))
                                          .GroupBy(x => x)
                                          .Where(x => x.Count() > 1)
                                          .Select(x => x.Key)
                                          .ToList();

            foreach (var duplicate in duplicateRepoNames)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"WARNING: DUPLICATE REPO NAME: {duplicate}");
                Console.ResetColor();
            }
        }

        private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo.Replace(" ", "-")}";

        private void GenerateScript(Dictionary<string, Dictionary<string, IEnumerable<string>>> repos,
                                          Dictionary<string, Dictionary<string, Dictionary<string, IEnumerable<string>>>> pipelines,
                                          Dictionary<string, string> appIds,
                                          string githubOrg,
                                          FileInfo output,
                                          bool skipIdp)
        {
            var content = new StringBuilder();

            foreach (var adoOrg in repos.Keys)
            {
                content.AppendLine($"# =========== Organization: {adoOrg} ===========");

                var hasAppId = appIds.TryGetValue(adoOrg, out var appId);

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
                            content.AppendLine(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo));
                            content.AppendLine(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo));
                            content.AppendLine(AutolinkScript(githubOrg, githubRepo, adoOrg, adoTeamProject));
                            content.AppendLine(GithubRepoPermissionsScript(adoTeamProject, githubOrg, githubRepo));

                            if (hasAppId)
                            {
                                foreach (var adoPipeline in pipelines[adoOrg][adoTeamProject][adoRepo])
                                {
                                    content.AppendLine(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId));
                                }
                            }
                        }

                        content.AppendLine();
                        content.AppendLine(BoardsIntegrationScript(adoOrg, adoTeamProject, githubOrg, repos[adoOrg][adoTeamProject].Select(x => GetGithubRepoName(adoTeamProject, x)).ToList()));
                    }
                }

                content.AppendLine();
                content.AppendLine();
            }

            File.WriteAllText(output.FullName, content.ToString());
        }

        private string DisableAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo)
        {
            return _reposOnly
                ? string.Empty
                : $"./octoshift disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"";
        }

        private string LockAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo)
        {
            return _reposOnly
                ? string.Empty
                : $"./octoshift lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"";
        }

        private string ShareServiceConnectionScript(string adoOrg, string adoTeamProject, string appId)
        {
            return _reposOnly
                ? string.Empty
                : $"./octoshift share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\"";
        }

        private string AutolinkScript(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject)
        {
            return _reposOnly
                ? string.Empty
                : $"./octoshift configure-autolink --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\"";
        }

        private string MigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo)
        {
            return $"./octoshift migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"";
        }

        private string CreateGithubTeamsScript(string adoTeamProject, string githubOrg, bool skipIdp)
        {
            if (_reposOnly)
            {
                return string.Empty;
            }

            var result = $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\"";

            if (!skipIdp)
            {
                result += $" --idp-group \"{adoTeamProject}-Maintainers\"";
            }
            result += Environment.NewLine;

            result += $"./octoshift create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\"";

            if (!skipIdp)
            {
                result += $" --idp-group \"{adoTeamProject}-Admins\"";
            }

            return result;
        }

        private string GithubRepoPermissionsScript(string adoTeamProject, string githubOrg, string githubRepo)
        {
            if (_reposOnly)
            {
                return string.Empty;
            }

            var result = $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\"";
            result += Environment.NewLine;
            result += $"./octoshift add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\"";

            return result;
        }

        private string RewireAzurePipelineScript(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string appId)
        {
            return _reposOnly
                ? string.Empty
                : $"./octoshift rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{adoPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --service-connection-id \"{appId}\"";
        }

        private string BoardsIntegrationScript(string adoOrg, string adoTeamProject, string githubOrg, IEnumerable<string> githubRepos)
        {
            if (_reposOnly)
            {
                return string.Empty;
            }

            var repoList = string.Join(",", githubRepos);
            return $"./octoshift integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repos \"{repoList}\"";
        }
    }
}