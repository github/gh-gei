using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class TeamProjectsCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;
        private readonly AdoApiFactory _adoApiFactory;

        public TeamProjectsCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory, AdoApiFactory adoApiFactory)
        {
            _adoInspectorServiceFactory = adoInspectorServiceFactory;
            _adoApiFactory = adoApiFactory;
        }

        public virtual async Task<string> Generate(string adoPat, bool minimal = false)
        {
            var adoApi = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.Append("org,teamproject,url");
            result.AppendLine(!minimal ? ",repo-count,pipeline-count,pr-count" : null);

            foreach (var org in await inspector.GetOrgs())
            {
                foreach (var teamProject in await inspector.GetTeamProjects(org))
                {
                    var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}";
                    var repoCount = !minimal ? await inspector.GetRepoCount(org, teamProject) : 0;
                    var pipelineCount = !minimal ? await inspector.GetPipelineCount(org, teamProject) : 0;
                    var prCount = !minimal ? await inspector.GetPullRequestCount(org, teamProject) : 0;
                    result.Append($"\"{org}\",\"{teamProject}\",\"{url}\"");
                    result.AppendLine(!minimal ? $",{repoCount},{pipelineCount},{prCount}" : null);
                }
            }

            return result.ToString();
        }
    }
}
