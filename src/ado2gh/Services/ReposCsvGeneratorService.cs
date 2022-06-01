using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;

        public ReposCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory) => _adoInspectorServiceFactory = adoInspectorServiceFactory;

        public virtual async Task<string> Generate(AdoApi adoApi)
        {
            if (adoApi is null)
            {
                throw new ArgumentNullException(nameof(adoApi));
            }

            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,url,pipeline-count,pr-count,last-push-date,commits-past-year");

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

                        result.AppendLine($"\"{org}\",\"{teamProject}\",\"{repo}\",\"{url}\",{pipelineCount},{prCount},\"{lastPushDate:dd-MMM-yyyy h:mm tt}\",{commitsPastYear}");
                    }
                }
            }

            return result.ToString();
        }
    }
}
