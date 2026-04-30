using System;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.GitlabToGithub.Factories;

namespace OctoshiftCLI.GitlabToGithub
{
    public class ReposCsvGeneratorService
    {
        private readonly GitlabInspectorServiceFactory _gitlabInspectorServiceFactory;
        private readonly GitlabApiFactory _gitlabApiFactory;

        public ReposCsvGeneratorService(GitlabInspectorServiceFactory gitlabInspectorServiceFactory, GitlabApiFactory gitlabApiFactory)
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

            result.Append("group-path,group-name,project,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes");
            result.AppendLine(!minimal ? ",is-archived,pr-count" : null);

            var groups = string.IsNullOrWhiteSpace(gitlabGroup) ? await inspector.GetGroups() : new[] { await inspector.GetGroup(gitlabGroup) };

            foreach (var (groupPath, groupName) in groups)
            {
                foreach (var project in await inspector.GetProjects(groupPath))
                {
                    var url = $"{gitlabServerUrl.TrimEnd('/')}/{groupPath}/{project.Path}";
                    var lastCommitDate = await gitlabApi.GetRepositoryLatestCommitDate(groupPath, project.Path);
                    var (repoSize, attachmentsSize) = await gitlabApi.GetRepositoryAndAttachmentsSize(groupPath, project.Path);
                    var prCount = !minimal ? await inspector.GetRepositoryPullRequestCount(groupPath, project.Path) : 0;

                    var group = groupName.Replace(",", Uri.EscapeDataString(","));
                    var projectName = project.Name.Replace(",", Uri.EscapeDataString(","));

                    if (lastCommitDate == null)
                    {
                        result.Append($"\"{groupPath}\",\"{group}\",\"{projectName}\",\"{url}\",,\"{repoSize:D}\",\"{attachmentsSize:D}\"");
                    }
                    else
                    {
                        result.Append($"\"{groupPath}\",\"{group}\",\"{projectName}\",\"{url}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{repoSize:D}\",\"{attachmentsSize:D}\"");
                    }

                    try
                    {
                        var archived = !minimal && await gitlabApi.GetIsRepositoryArchived(groupPath, project.Path);
                        result.AppendLine(!minimal ? $",\"{archived}\",{prCount}" : null);
                    }
                    catch (ArgumentNullException)
                    {
                        // The archived field was introduced in BBS 6.0.0
                        result.Replace(",is-archived", null);
                        result.AppendLine(!minimal ? $",{prCount}" : null);
                    }
                }
            }

            return result.ToString();
        }
    }
}
