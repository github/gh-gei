using System.Collections.Generic;
using System.Text;

namespace OctoshiftCLI.AdoToGithub
{
    public class PipelinesCsvGeneratorService
    {
        public virtual string Generate(IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo,pipeline");

            if (pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    foreach (var teamProject in pipelines[org].Keys)
                    {
                        foreach (var repo in pipelines[org][teamProject].Keys)
                        {
                            foreach (var pipeline in pipelines[org][teamProject][repo])
                            {
                                result.AppendLine($"{org},{teamProject},{repo},{pipeline}");
                            }
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
