using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class PipelinesCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;

        public PipelinesCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory) => _adoInspectorServiceFactory = adoInspectorServiceFactory;

        public virtual async Task<string> Generate(AdoApi adoApi)
        {
            if (adoApi is null)
            {
                throw new ArgumentNullException(nameof(adoApi));
            }

            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,pipeline,url");

            foreach (var org in await inspector.GetOrgs())
            {
                foreach (var teamProject in await inspector.GetTeamProjects(org))
                {
                    foreach (var repo in await inspector.GetRepos(org, teamProject))
                    {
                        foreach (var pipeline in await inspector.GetPipelines(org, teamProject, repo))
                        {
                            var pipelineId = await adoApi.GetPipelineId(org, teamProject, pipeline);
                            var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_build?definitionId={pipelineId}";
                            result.AppendLine($"\"{org}\",\"{teamProject}\",\"{repo}\",\"{pipeline}\",\"{url}\"");
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
