using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class OrgsCsvGeneratorService
    {
        public virtual async Task<string> Generate(
            AdoApi ado,
            IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> pipelines)
        {
            var result = new StringBuilder();

            result.AppendLine("name,owner,teamproject-count,repo-count,pipeline-count");

            if (ado != null && pipelines != null)
            {
                foreach (var org in pipelines.Keys)
                {
                    var owner = await ado.GetOrgOwner(org);
                    var teamProjectCount = pipelines[org].Count;
                    var repoCount = pipelines[org].Sum(tp => tp.Value.Count);
                    var pipelineCount = pipelines[org].Sum(tp => tp.Value.Sum(repo => repo.Value.Count()));

                    result.AppendLine($"{org},{owner},{teamProjectCount},{repoCount},{pipelineCount}");
                }
            }

            return result.ToString();
        }
    }
}
