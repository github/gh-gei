using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class OrgsCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;
        private readonly AdoApiFactory _adoApiFactory;

        public OrgsCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory, AdoApiFactory adoApiFactory)
        {
            _adoInspectorServiceFactory = adoInspectorServiceFactory;
            _adoApiFactory = adoApiFactory;
        }

        public virtual async Task<string> Generate(string adoPat)
        {
            var adoApi = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("name,url,owner,teamproject-count,repo-count,pipeline-count,pr-count,is-pat-org-admin");

            foreach (var org in await inspector.GetOrgs())
            {
                var owner = await adoApi.GetOrgOwner(org);
                var teamProjectCount = await inspector.GetTeamProjectCount(org);
                var repoCount = await inspector.GetRepoCount(org);
                var pipelineCount = await inspector.GetPipelineCount(org);
                var prCount = await inspector.GetPullRequestCount(org);
                var isOrgAdmin = await adoApi.IsCallerOrgAdmin(org);
                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";

                result.AppendLine($"\"{org}\",\"{url}\",\"{owner}\",{teamProjectCount},{repoCount},{pipelineCount},{prCount},{isOrgAdmin}");
            }

            return result.ToString();
        }
    }
}
