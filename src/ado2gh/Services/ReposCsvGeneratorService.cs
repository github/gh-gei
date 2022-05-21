using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        public virtual string Generate(IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,pipeline-count");

            if (pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    foreach (var teamProject in pipelines[org].Keys)
                    {
                        foreach (var repo in pipelines[org][teamProject].Keys)
                        {
                            var pipelineCount = pipelines[org][teamProject][repo].Count();
                            result.AppendLine($"{org},{teamProject},{repo},{pipelineCount}");
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
