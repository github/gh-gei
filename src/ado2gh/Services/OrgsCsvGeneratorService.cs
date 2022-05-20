using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub
{
    public class OrgsCsvGeneratorService
    {
        public virtual async Task<string> Generate(AdoApi ado, IEnumerable<string> orgs)
        {
            var result = new StringBuilder();

            result.AppendLine("name,owner");

            if (ado != null && orgs != null)
            {
                foreach (var org in orgs)
                {
                    var owner = await ado.GetOrgOwner(org);
                    result.AppendLine($"{org},{owner}");
                }
            }

            return result.ToString();
        }
    }
}
