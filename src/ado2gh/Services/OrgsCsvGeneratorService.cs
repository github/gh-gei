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

        public virtual async Task<string> Generate(string adoPat, bool minimal = false)
        {
            var adoApi = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.Append("name,url,owner,teamproject-count,repo-count,pipeline-count");
            result.AppendLine(!minimal ? ",pr-count" : null);

            foreach (var org in await inspector.GetOrgs())
            {
                var owner = await adoApi.GetOrgOwner(org);
                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}";
                var teamProjectCount = await inspector.GetTeamProjectCount(org);
                var repoCount = await inspector.GetRepoCount(org);
                var pipelineCount = await inspector.GetPipelineCount(org);
                var prCount = !minimal ? await inspector.GetPullRequestCount(org) : 0;

                result.Append($"\"{org}\",\"{url}\",\"{owner}\",{teamProjectCount},{repoCount},{pipelineCount}");
                result.AppendLine(!minimal ? $",{prCount}" : null);
            }

            return result.ToString();
        }
    }
}
