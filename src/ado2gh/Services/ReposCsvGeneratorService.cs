using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        public virtual string Generate(IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,url,pipeline-count");

            if (pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    foreach (var teamProject in pipelines[org].Keys)
                    {
                        foreach (var repo in pipelines[org][teamProject].Keys)
                        {
                            var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_git/{Uri.EscapeDataString(repo)}";
                            var pipelineCount = pipelines[org][teamProject][repo].Count();
                            result.AppendLine($"{org},{teamProject},{repo},{url},{pipelineCount}");
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
