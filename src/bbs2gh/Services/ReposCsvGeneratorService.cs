using System;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Factories;

namespace OctoshiftCLI.BbsToGithub
{
    public class ReposCsvGeneratorService
    {
        private readonly BbsInspectorServiceFactory _bbsInspectorServiceFactory;
        private readonly BbsApiFactory _bbsApiFactory;

        public ReposCsvGeneratorService(BbsInspectorServiceFactory bbsInspectorServiceFactory, BbsApiFactory bbsApiFactory)
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

            result.Append("project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes");
            result.AppendLine(!minimal ? ",is-archived,pr-count" : null);

            var projects = string.IsNullOrWhiteSpace(bbsProject) ? await inspector.GetProjects() : new[] { await inspector.GetProject(bbsProject) };

            foreach (var (projectKey, projectName) in projects)
            {
                foreach (var repo in await inspector.GetRepos(projectKey))
                {
                    var url = $"{bbsServerUrl.TrimEnd('/')}/projects/{projectKey}/repos/{Uri.EscapeDataString(repo.Slug)}";
                    var lastCommitDate = await inspector.GetLastCommitDate(projectKey, repo.Slug);
                    var (repoSize, attachmentsSize) = await bbsApi.GetRepositoryAndAttachmentsSize(projectKey, repo.Slug, bbsUsername, bbsPassword);
                    var prCount = !minimal ? await inspector.GetRepositoryPullRequestCount(projectKey, repo.Slug) : 0;

                    var project = projectName.Replace(",", Uri.EscapeDataString(","));
                    var repoName = repo.Name.Replace(",", Uri.EscapeDataString(","));

                    result.Append($"\"{projectKey}\",\"{project}\",\"{repoName}\",\"{url}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{repoSize:D}\",\"{attachmentsSize:D}\"");

                    try
                    {
                        var archived = !minimal && await bbsApi.GetIsRepositoryArchived(projectKey, repo.Slug);
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
