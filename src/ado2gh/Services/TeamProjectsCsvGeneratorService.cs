using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OctoshiftCLI.AdoToGithub
{
    public class TeamProjectsCsvGeneratorService
    {
        public virtual string Generate(IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,url,repo-count,pipeline-count");

            if (pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    foreach (var teamProject in pipelines[org].Keys)
                    {
                        var repoCount = pipelines[org][teamProject].Count;
                        var pipelineCount = pipelines[org][teamProject].Sum(repo => repo.Value.Count());
                        var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}";
                        result.AppendLine($"{org},{teamProject},{url},{repoCount},{pipelineCount}");
                    }
                }
            }

            return result.ToString();
        }
    }
}
