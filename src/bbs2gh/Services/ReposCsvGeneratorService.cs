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

        public virtual async Task<string> Generate(string bbsServerUrl, string bbsProject, string bbsUsername, string bbsPassword, bool noSslVerify, bool minimal = false)
        {
            bbsServerUrl = bbsServerUrl ?? throw new ArgumentNullException(nameof(bbsServerUrl));
            bbsUsername = bbsUsername ?? throw new ArgumentNullException(nameof(bbsUsername));
            bbsPassword = bbsPassword ?? throw new ArgumentNullException(nameof(bbsPassword));

            var bbsApi = _bbsApiFactory.Create(bbsServerUrl, bbsUsername, bbsPassword, noSslVerify);
            var inspector = _bbsInspectorServiceFactory.Create(bbsApi);
            var result = new StringBuilder();

            result.Append("project,repo,url,last-commit-date,compressed-repo-size-in-bytes");
            result.AppendLine(!minimal ? ",is-archived,pr-count" : null);

            var projects = string.IsNullOrEmpty(bbsProject) ? await inspector.GetProjects() : new[] { bbsProject };

            foreach (var project in projects)
            {
                foreach (var repo in await inspector.GetRepos(project))
                {
                    var url = $"{bbsServerUrl.TrimEnd('/')}/projects/{project}/repos/{repo.Name}";
                    var lastCommitDate = await inspector.GetLastCommitDate(project, repo.Name);
                    var repoSize = await bbsApi.GetRepositorySize(project, repo.Name);

                    var archived = !minimal && await bbsApi.GetIsRepositoryArchived(project, repo.Name);
                    var prCount = !minimal ? await inspector.GetRepositoryPullRequestCount(project, repo.Name) : 0;

                    result.Append($"\"{project}\",\"{repo.Name}\",\"{url}\",\"{lastCommitDate:dd-MMM-yyyy hh:mm tt}\",\"{repoSize:N0}\"");
                    result.AppendLine(!minimal ? $",\"{archived}\",{prCount}" : null);
                }
            }

            return result.ToString();
        }
    }
}
