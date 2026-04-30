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

        public virtual async Task<string> Generate(string gitlabServerUrl, string gitlabUsername, string gitlabPassword, bool noSslVerify, string gitlabGroup = "", bool minimal = false)
        {
            gitlabServerUrl = gitlabServerUrl ?? throw new ArgumentNullException(nameof(gitlabServerUrl));

            var gitlabApi = _gitlabApiFactory.Create(gitlabServerUrl, gitlabUsername, gitlabPassword, noSslVerify);
            var inspector = _gitlabInspectorServiceFactory.Create(gitlabApi);
            var result = new StringBuilder();

            result.Append("project-key,project-name,url,repo-count");
            result.AppendLine(!minimal ? ",mr-count" : null);

            var projects = string.IsNullOrWhiteSpace(gitlabGroup) ? await inspector.GetGroups() : new[] { await inspector.GetGroup(gitlabGroup) };

            foreach (var (Key, Name) in projects)
            {
                var url = $"{gitlabServerUrl.TrimEnd('/')}/projects/{Uri.EscapeDataString(Key)}";
                var repoCount = await inspector.GetProjectCount(Key);
                var mrCount = !minimal ? await inspector.GetMergeRequestCount(Key) : 0;

                var projectName = Name.Replace(",", Uri.EscapeDataString(","));

                result.Append($"\"{Key}\",\"{projectName}\",\"{url}\",{repoCount}");
                result.AppendLine(!minimal ? $",{mrCount}" : null);
            }

            return result.ToString();
        }
    }
}
