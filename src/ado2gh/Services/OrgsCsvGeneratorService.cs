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
                var teamProjectCount = await _adoInspectorService.GetTeamProjectCount(org);
                var repoCount = await _adoInspectorService.GetRepoCount(org);
                var pipelineCount = await _adoInspectorService.GetPipelineCount(org);
                var prCount = _adoInspectorService.GetPullRequestCount(org);
                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";

                result.AppendLine($"\"{org}\",\"{url}\",\"{owner}\",\"{teamProjectCount}\",\"{repoCount}\",\"{pipelineCount}\",\"{prCount}\"");
            }

            return result.ToString();
        }
    }
}
