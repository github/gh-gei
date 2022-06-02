using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class PipelinesCsvGeneratorService
    {
        public virtual async Task<string> Generate(AdoApi ado, IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,pipeline,url");

            if (ado != null & pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    foreach (var teamProject in pipelines[org].Keys)
                    {
                        foreach (var repo in pipelines[org][teamProject].Keys)
                        {
                            foreach (var pipeline in pipelines[org][teamProject][repo])
                            {
                                var pipelineId = await ado.GetPipelineId(org, teamProject, pipeline);
                                var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_build?definitionId={pipelineId}";
                                result.AppendLine($"{org},{teamProject},{repo},{pipeline},{url}");
                            }
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
