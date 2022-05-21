using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        public virtual async Task<string> Generate(AdoApi ado, IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,url,pipeline-count,pr-count");

            if (ado != null && pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    foreach (var teamProject in pipelines[org].Keys)
                    {
                        foreach (var repo in pipelines[org][teamProject].Keys)
                        {
                            var url = $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(teamProject)}/_git/{Uri.EscapeDataString(repo)}";
                            var pipelineCount = pipelines[org][teamProject][repo].Count();

                            var repoId = await ado.GetRepoId(org, teamProject, repo);
                            var prCount = (await ado.GetPullRequests(org, teamProject, repoId)).Count();

                            result.AppendLine($"{org},{teamProject},{repo},{url},{pipelineCount},{prCount}");
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
