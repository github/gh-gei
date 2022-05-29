using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class OrgsCsvGeneratorService
    {
        private readonly AdoInspectorService _adoInspectorService;

        public OrgsCsvGeneratorService(AdoInspectorService adoInspectorService)
        {
            _adoInspectorService = adoInspectorService;
        }

        public virtual async Task<string> Generate(AdoApi adoApi)
        {
            if (adoApi is null)
            {
                throw new ArgumentNullException(nameof(adoApi));
            }

            var result = new StringBuilder();

            result.AppendLine("name,url,owner,teamproject-count,repo-count,pipeline-count,pr-count");

            foreach (var org in await _adoInspectorService.GetOrgs())
            {
                var owner = await adoApi.GetOrgOwner(org);
                var teamProjects = await _adoInspectorService.GetTeamProjects(org);
                var teamProjectCount = teamProjects.Count();
                var repoCount = teamProjects.SelectMany(tp => _adoInspectorService.GetRepos(org, tp).Result).Count();
                var pipelineCount = teamProjects.SelectMany(tp => _adoInspectorService.GetRepos(org, tp).Result
                                                                .SelectMany(r => _adoInspectorService.GetPipelines(org, tp, r).Result)).Count();
                var prCount = teamProjects.Sum(tp => _adoInspectorService.GetRepos(org, tp).Result.Sum(r => _adoInspectorService.GetPullRequestCount(org, tp, r).Result));
                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";

                result.AppendLine($"\"{org}\",\"{url}\",\"{owner}\",\"{teamProjectCount}\",\"{repoCount}\",\"{pipelineCount}\",\"{prCount}\"");
            }

            return result.ToString();
        }
    }
}
