using System;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Factories;

namespace OctoshiftCLI.BbsToGithub
{
    public class ProjectsCsvGeneratorService
    {
        private readonly BbsInspectorServiceFactory _bbsInspectorServiceFactory;
        private readonly BbsApiFactory _bbsApiFactory;

        public ProjectsCsvGeneratorService(BbsInspectorServiceFactory bbsInspectorServiceFactory, BbsApiFactory bbsApiFactory)
        {
            _bbsInspectorServiceFactory = bbsInspectorServiceFactory;
            _bbsApiFactory = bbsApiFactory;
        }

        public virtual async Task<string> Generate(string bbsServerUrl, string bbsUsername, string bbsPassword, bool noSslVerify, string bbsProject = "", bool minimal = false)
        {
            bbsServerUrl = bbsServerUrl ?? throw new ArgumentNullException(nameof(bbsServerUrl));

            var bbsApi = _bbsApiFactory.Create(bbsServerUrl, bbsUsername, bbsPassword, noSslVerify);
            var inspector = _bbsInspectorServiceFactory.Create(bbsApi);
            var result = new StringBuilder();

            result.Append("project-key,project-name,url,repo-count");
            result.AppendLine(!minimal ? ",pr-count" : null);

            var projects = string.IsNullOrWhiteSpace(bbsProject) ? await inspector.GetProjects() : new[] { await inspector.GetProject(bbsProject) };

            foreach (var (Key, Name) in projects)
            {
                var url = $"{bbsServerUrl.TrimEnd('/')}/projects/{Uri.EscapeDataString(Key)}";
                var repoCount = await inspector.GetRepoCount(Key);
                var prCount = !minimal ? await inspector.GetPullRequestCount(Key) : 0;

                var projectName = Name.Replace(",", Uri.EscapeDataString(","));

                result.Append($"\"{Key}\",\"{projectName}\",\"{url}\",{repoCount}");
                result.AppendLine(!minimal ? $",{prCount}" : null);
            }

            return result.ToString();
        }
    }
}
