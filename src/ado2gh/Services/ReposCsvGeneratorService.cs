using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        private readonly AdoInspectorService _adoInspectorService;

        public ReposCsvGeneratorService(AdoInspectorService adoInspectorService)
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

            result.AppendLine("org,teamproject,repo,url,pipeline-count,pr-count");

            foreach (var org in await _adoInspectorService.GetOrgs())
            {
                foreach (var teamProject in await _adoInspectorService.GetTeamProjects(org))
                {
                    foreach (var repo in await _adoInspectorService.GetRepos(org, teamProject))
                    {
                        var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_git/{Uri.EscapeDataString(repo)}";
                        var pipelineCount = await _adoInspectorService.GetPipelineCount(org, teamProject, repo);
                        var prCount = await _adoInspectorService.GetPullRequestCount(org, teamProject, repo);

                        result.AppendLine($"\"{org}\",\"{teamProject}\",\"{repo}\",\"{url}\",{pipelineCount},{prCount}");
                    }
                }
            }

            return result.ToString();
        }
    }
}
