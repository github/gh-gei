using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class TeamProjectsCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;

        public TeamProjectsCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory) => _adoInspectorServiceFactory = adoInspectorServiceFactory;

        public virtual async Task<string> Generate(AdoApi adoApi)
        {
            if (adoApi is null)
            {
                throw new ArgumentNullException(nameof(adoApi));
            }

            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,url,repo-count,pipeline-count,pr-count");

            foreach (var org in await inspector.GetOrgs())
            {
                foreach (var teamProject in await inspector.GetTeamProjects(org))
                {
                    var repoCount = await inspector.GetRepoCount(org, teamProject);
                    var pipelineCount = await inspector.GetPipelineCount(org, teamProject);
                    var prCount = await inspector.GetPullRequestCount(org, teamProject);
                    var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}";
                    result.AppendLine($"\"{org}\",\"{teamProject}\",\"{url}\",{repoCount},{pipelineCount},{prCount}");
                }
            }

            return result.ToString();
        }
    }
}
