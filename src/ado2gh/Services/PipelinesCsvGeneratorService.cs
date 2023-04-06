using System;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Factories;

namespace OctoshiftCLI.AdoToGithub
{
    public class PipelinesCsvGeneratorService
    {
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;
        private readonly AdoApiFactory _adoApiFactory;

        public PipelinesCsvGeneratorService(AdoInspectorServiceFactory adoInspectorServiceFactory, AdoApiFactory adoApiFactory)
        {
            _adoInspectorServiceFactory = adoInspectorServiceFactory;
            _adoApiFactory = adoApiFactory;
        }

        public virtual async Task<string> Generate(string adoPat)
        {
            var adoApi = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(adoApi);
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,pipeline,url");

            foreach (var org in await inspector.GetOrgs())
            {
                foreach (var teamProject in await inspector.GetTeamProjects(org))
                {
                    foreach (var repo in await inspector.GetRepos(org, teamProject))
                    {
                        foreach (var pipeline in await inspector.GetPipelines(org, teamProject, repo.Name))
                        {
                            var pipelineId = await adoApi.GetPipelineId(org, teamProject, pipeline);
                            var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_build?definitionId={pipelineId}";
                            result.AppendLine($"\"{org}\",\"{teamProject}\",\"{repo.Name}\",\"{pipeline}\",\"{url}\"");
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
