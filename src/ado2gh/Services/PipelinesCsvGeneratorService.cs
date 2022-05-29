using System;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class PipelinesCsvGeneratorService
    {
        private readonly AdoInspectorService _adoInspectorService;

        public PipelinesCsvGeneratorService(AdoInspectorService adoInspectorService)
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

            result.AppendLine("org,teamproject,repo,pipeline,url");

            foreach (var org in await _adoInspectorService.GetOrgs())
            {
                foreach (var teamProject in await _adoInspectorService.GetTeamProjects(org))
                {
                    foreach (var repo in await _adoInspectorService.GetRepos(org, teamProject))
                    {
                        foreach (var pipeline in await _adoInspectorService.GetPipelines(org, teamProject, repo))
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
