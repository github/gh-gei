using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Services
{
    public class ReposCsvGeneratorService
    {
        private readonly GithubInspectorServiceFactory _githubInspectorServiceFactory;
        private readonly ISourceGithubApiFactory _githubApiFactory;
        private readonly OctoLogger _log;

        public ReposCsvGeneratorService(OctoLogger log, GithubInspectorServiceFactory githubInspectorServiceFactory, ISourceGithubApiFactory githubApiFactory)
        {
            _log = log;
            _githubInspectorServiceFactory = githubInspectorServiceFactory;
            _githubApiFactory = githubApiFactory;
        }

        public virtual async Task<string> Generate(string apiUrl, string githubPat, string org, bool minimal = false)
        {
            var githubApi = _githubApiFactory.Create(apiUrl, githubPat);
            var inspector = _githubInspectorServiceFactory.Create(githubApi);
            var result = new StringBuilder();

            result.Append("org,repo,url,visibility,last-push-date,compressed-repo-size-in-bytes");
            result.AppendLine(!minimal ? ",most-active-contributor,pr-count,commits-on-default-branch" : null);

            foreach (var (repoName, repoVisibility, repoSize) in await inspector.GetRepos(org))
            {
                _log.LogInformation($"Repo: {repoName}");

                if (await githubApi.IsRepoEmpty(org, repoName))
                {
                    _log.LogWarning($"Skipping empty repo {repoName}");
                }
                else
                {
                    var baseUrl = apiUrl.HasValue() ? ExtractGhesBaseUrl(apiUrl) : "https://github.com";
                    var url = $"{baseUrl}/{Uri.EscapeDataString(org)}/_git/{Uri.EscapeDataString(repoName)}";
                    var lastPushDate = await githubApi.GetLastCommitDateOnDefaultBranch(org, repoName);
                    var mostActiveContributor = !minimal ? await GetMostActiveContributor(org, repoName, githubApi) : null;
                    var prCount = !minimal ? await inspector.GetPullRequestCount(org, repoName) : 0;
                    var commitsPastYear = !minimal ? await githubApi.GetCommitCount(org, repoName) : 0;

                    result.Append($"\"{org}\",\"{repoName}\",\"{url}\",\"{repoVisibility}\",\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",\"{repoSize:N0}\"");
                    result.AppendLine(!minimal ? $",\"{mostActiveContributor}\",{prCount},{commitsPastYear}" : null);
                }
            }

            return result.ToString();
        }

        private string ExtractGhesBaseUrl(string ghesApiUrl)
        {
            // We expect the GHES url template to be either http(s)://hostname/api/v3 or http(s)://api.hostname.com.
            // We are either going to be able to extract and return the base url based on the above templates or 
            // will fallback to ghesApiUrl and return it as the base url. 

            ghesApiUrl = ghesApiUrl.Trim().TrimEnd('/');

            var baseUrl = Regex.Match(ghesApiUrl, @"(?<baseUrl>https?:\/\/.+)\/api\/v3", RegexOptions.IgnoreCase).Groups["baseUrl"].Value;
            if (baseUrl.HasValue())
            {
                return baseUrl;
            }

            var match = Regex.Match(ghesApiUrl, @"(?<scheme>https?):\/\/api\.(?<host>.+)", RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups["scheme"]}://{match.Groups["host"]}" : ghesApiUrl;
        }

        private async Task<string> GetMostActiveContributor(string org, string repo, GithubApi githubApi)
        {
            var pushers = await githubApi.GetAuthorsSince(org, repo, DateTime.Today.AddMonths(-1));
            pushers = pushers.Where(x => !x.Contains("Service"));
            var mostActiveContributor = pushers.Any() ? pushers.GroupBy(x => x)
                                                               .OrderByDescending(x => x.Count())
                                                               .First()
                                                               .First()
                                                      : "N/A";

            return mostActiveContributor;
        }
    }
}
