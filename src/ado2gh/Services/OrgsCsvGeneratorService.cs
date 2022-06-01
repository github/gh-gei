using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class OrgsCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;

        public OrgsCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory) => _adoInspectorServiceFactory = adoInspectorServiceFactory;

        public virtual async Task<string> Generate(AdoApi adoApi)
        {
            if (adoApi is null)
            {
                throw new ArgumentNullException(nameof(adoApi));
            }

            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("name,url,owner,teamproject-count,repo-count,pipeline-count,pr-count");

            foreach (var org in await inspector.GetOrgs())
            {
                var owner = await adoApi.GetOrgOwner(org);
                var teamProjectCount = await inspector.GetTeamProjectCount(org);
                var repoCount = await inspector.GetRepoCount(org);
                var pipelineCount = await inspector.GetPipelineCount(org);
                var prCount = await inspector.GetPullRequestCount(org);
                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";

                result.AppendLine($"\"{org}\",\"{url}\",\"{owner}\",{teamProjectCount},{repoCount},{pipelineCount},{prCount}");
            }

            return result.ToString();
        }
    }
}
