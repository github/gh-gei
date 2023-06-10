using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub
{
    public class ProjectsCsvGeneratorService
    {
        private readonly BbsApi _bbsApi;
        private readonly BbsInspectorServiceFactory _bbsInspectorServiceFactory;

        public ProjectsCsvGeneratorService(BbsApi bbsApi, BbsInspectorServiceFactory bbsInspectorServiceFactory)
        {
            _bbsApi = bbsApi;
            _bbsInspectorServiceFactory = bbsInspectorServiceFactory;
        }

        public virtual async Task<string> Generate(string bbsServerUrl, string bbsProject, bool minimal = false)
        {
            var inspector = _bbsInspectorServiceFactory.Create(_bbsApi);
            var result = new StringBuilder();

            result.Append("name,url,repo-count");
            result.AppendLine(!minimal ? ",pr-count" : null);

            var projects = string.IsNullOrEmpty(bbsProject) ? await inspector.GetProjects() : new[] { bbsProject };

            foreach (var project in projects)
            {
                var url = $"{bbsServerUrl.TrimEnd('/')}/projects/{project}";
                var repoCount = await inspector.GetRepoCount(project);
                var prCount = !minimal ? await inspector.GetPullRequestCount(project) : 0;

                result.Append($"\"{project}\",\"{url}\",{repoCount}");
                result.AppendLine(!minimal ? $",{prCount}" : null);
            }

            return result.ToString();
        }
    }
}
