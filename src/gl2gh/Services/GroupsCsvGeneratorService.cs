using System;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.GitlabToGithub.Factories;

namespace OctoshiftCLI.GitlabToGithub
{
    public class GroupsCsvGeneratorService
    {
        private readonly GitlabInspectorServiceFactory _gitlabInspectorServiceFactory;
        private readonly GitlabApiFactory _gitlabApiFactory;

        public GroupsCsvGeneratorService(GitlabInspectorServiceFactory gitlabInspectorServiceFactory, GitlabApiFactory gitlabApiFactory)
        {
            _gitlabInspectorServiceFactory = gitlabInspectorServiceFactory;
            _gitlabApiFactory = gitlabApiFactory;
        }

        public virtual async Task<string> Generate(string gitlabServerUrl, string gitlabPat, bool noSslVerify, string gitlabGroup = "", bool minimal = false)
        {
            gitlabServerUrl = gitlabServerUrl ?? throw new ArgumentNullException(nameof(gitlabServerUrl));

            var gitlabApi = _gitlabApiFactory.Create(gitlabServerUrl, gitlabPat, noSslVerify);
            var inspector = _gitlabInspectorServiceFactory.Create(gitlabApi);
            var result = new StringBuilder();

            result.Append("group-path,group-name,url,project-count");
            result.AppendLine(!minimal ? ",mr-count" : null);

            var groups = string.IsNullOrWhiteSpace(gitlabGroup) ? await inspector.GetGroups() : new[] { await inspector.GetGroup(gitlabGroup) };

            foreach (var (groupPath, groupName) in groups)
            {
                var url = $"{gitlabServerUrl.TrimEnd('/')}/{groupPath}";
                var projectCount = await inspector.GetProjectCount(groupPath);
                var mrCount = !minimal ? await inspector.GetMergeRequestCount(groupPath) : 0;

                var name = groupName.Replace(",", Uri.EscapeDataString(","));

                result.Append($"\"{groupPath}\",\"{name}\",\"{url}\",{projectCount}");
                result.AppendLine(!minimal ? $",{mrCount}" : null);
            }

            return result.ToString();
        }
    }
}
