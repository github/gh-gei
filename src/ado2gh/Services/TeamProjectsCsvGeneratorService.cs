using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class TeamProjectsCsvGeneratorService
    {
        private readonly AdoInspectorService _adoInspectorService;

        public TeamProjectsCsvGeneratorService(AdoInspectorService adoInspectorService)
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

            result.AppendLine("org,teamproject,url,repo-count,pipeline-count,pr-count");

            foreach (var org in await _adoInspectorService.GetOrgs())
            {
                foreach (var teamProject in await _adoInspectorService.GetTeamProjects(org))
                {
                    var repos = await _adoInspectorService.GetRepos(org, teamProject);
                    var repoCount = repos.Count();
                    var pipelineCount = repos.SelectMany(r => _adoInspectorService.GetPipelines(org, teamProject, r).Result).Count();
                    var prCount = repos.Sum(r => _adoInspectorService.GetPullRequestCount(org, teamProject, r).Result);
                    var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}";
                    result.AppendLine($"\"{org}\",\"{teamProject}\",\"{url}\",\"{repoCount}\",\"{pipelineCount}\",\"{prCount}\"");
                }
            }

            return result.ToString();
        }
    }
}
