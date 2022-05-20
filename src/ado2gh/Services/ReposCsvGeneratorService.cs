using System.Collections.Generic;
using System.Text;

namespace OctoshiftCLI.AdoToGithub
{
    public class ReposCsvGeneratorService
    {
        public virtual string Generate(IDictionary<string, IDictionary<string, IEnumerable<string>>> repos)
        {
            var result = new StringBuilder();

            result.AppendLine("org,teamproject,repo");

            if (repos != null)
            {
                foreach (var org in repos.Keys)
                {
                    foreach (var teamProject in repos[org].Keys)
                    {
                        foreach (var repo in repos[org][teamProject])
                        {
                            result.AppendLine($"{org},{teamProject},{repo}");
                        }
                    }
                }
            }

            return result.ToString();
        }
    }
}
