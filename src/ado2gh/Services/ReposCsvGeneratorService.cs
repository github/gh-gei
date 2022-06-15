using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public virtual async Task<string> Generate(string adoPat)
        {
            var adoApi = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,url,pipeline-count,pr-count,last-push-date,commits-past-year,most-active-contributor");

            foreach (var org in await inspector.GetOrgs())
            {
                foreach (var teamProject in await inspector.GetTeamProjects(org))
                {
                    foreach (var repo in await inspector.GetRepos(org, teamProject))
                    {
                        var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_git/{Uri.EscapeDataString(repo)}";
                        var pipelineCount = await inspector.GetPipelineCount(org, teamProject, repo);
                        var prCount = await inspector.GetPullRequestCount(org, teamProject, repo);
                        var lastPushDate = await adoApi.GetLastPushDate(org, teamProject, repo);
                        var commitsPastYear = await adoApi.GetCommitCountSince(org, teamProject, repo, DateTime.Today.AddYears(-1));
                        var mostActiveContributor = await GetMostActiveContributor(org, teamProject, repo, adoApi);

                        result.AppendLine($"\"{org}\",\"{teamProject}\",\"{repo}\",\"{url}\",{pipelineCount},{prCount},\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",{commitsPastYear},\"{mostActiveContributor}\"");
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
