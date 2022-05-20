using System.Collections.Generic;
using System.Text;

namespace OctoshiftCLI.AdoToGithub
{
    public class TeamProjectsCsvGeneratorService
    {
        public virtual string Generate(AdoApi ado, IDictionary<string, IEnumerable<string>> teamProjects)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject");

            if (ado != null && teamProjects != null)
            {
                foreach (var org in teamProjects.Keys)
                {
                    foreach (var teamProject in teamProjects[org])
                    {
                        result.AppendLine($"{org},{teamProject}");
                    }
                }
            }

            return result.ToString();
        }
    }
}
