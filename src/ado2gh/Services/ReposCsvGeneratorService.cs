using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;
        private readonly AdoApiFactory _adoApiFactory;

        public ReposCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory, AdoApiFactory adoApiFactory)
        {
            _adoInspectorServiceFactory = adoInspectorServiceFactory;
            _adoApiFactory = adoApiFactory;
        }

        public virtual async Task<string> Generate(string adoPat, bool minimal = false)
        {
            var adoApi = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.Append("org,teamproject,repo,url,last-push-date,pipeline-count,compressed-repo-size-in-bytes");
            result.AppendLine(!minimal ? ",most-active-contributor,pr-count,commits-past-year" : null);

            foreach (var org in await inspector.GetOrgs())
            {
                foreach (var teamProject in await inspector.GetTeamProjects(org))
                {
                    foreach (var repo in await inspector.GetRepos(org, teamProject))
                    {
                        var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_git/{Uri.EscapeDataString(repo.Name)}";
                        var lastPushDate = await adoApi.GetLastPushDate(org, teamProject, repo.Name);
                        var pipelineCount = await inspector.GetPipelineCount(org, teamProject, repo.Name);
                        var mostActiveContributor = !minimal ? await GetMostActiveContributor(org, teamProject, repo.Name, adoApi) : null;
                        var prCount = !minimal ? await inspector.GetPullRequestCount(org, teamProject, repo.Name) : 0;
                        var commitsPastYear = !minimal ? await adoApi.GetCommitCountSince(org, teamProject, repo.Name, DateTime.Today.AddYears(-1)) : 0;

                        result.Append($"\"{org}\",\"{teamProject}\",\"{repo.Name}\",\"{url}\",\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",{pipelineCount},\"{repo.Size:N0}\"");
                        result.AppendLine(!minimal ? $",\"{mostActiveContributor}\",{prCount},{commitsPastYear}" : null);
                    }
                }
            }

            return result.ToString();
        }

        private async Task<string> GetMostActiveContributor(string org, string teamProject, string repo, AdoApi adoApi)
        {
            var pushers = await adoApi.GetPushersSince(org, teamProject, repo, DateTime.Today.AddYears(-1));
            pushers = pushers.Where(x => !x.Contains("Service"));
            var mostActiveContributor = pushers.Any() ? pushers.GroupBy(x => x)
                                                               .OrderByDescending(x => x.Count())
                                                               .First()
                                                               .First()
                                                      : "N/A";

            return mostActiveContributor;
        }
    }
}
