using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Services
{
    public class ReposCsvGeneratorService
    {
        private readonly GithubApi _githubApi;
        private readonly OctoLogger _log;

        public ReposCsvGeneratorService(OctoLogger log, GithubApi githubApi)
        {
            _log = log;
            _githubApi = githubApi;
        }

        public virtual async Task<string> Generate(string apiUrl, string githubPat, string org, bool minimal = false)
        {
            var result = new StringBuilder();

            _log.LogInformation("Finding Repos...");
            var repos = await _githubApi.GetRepos(org);
            _log.LogInformation($"Found {repos.Count()} Repos");

            result.Append("org,repo,url,visibility,last-push-date,compressed-repo-size-in-bytes,pr-count,commits-on-default-branch");
            result.AppendLine(!minimal ? ",most-active-contributor" : null);

            foreach (var (repoName, repoVisibility, repoSize) in repos)
            {

                try
                {
                    var (isRepoEmpty, commitCount, lastCommitDate) = await _githubApi.GetCommitInfo(org, repoName);

                    if (isRepoEmpty)
                    {
                        _log.LogWarning($"Skipping {repoName} because it is empty");
                    }
                    else
                    {
                        var baseUrl = apiUrl.HasValue() ? ExtractGhesBaseUrl(apiUrl) : "https://github.com";
                        var url = $"{baseUrl}/{org.EscapeDataString()}/_git/{repoName.EscapeDataString()}";
                        var mostActiveContributor = !minimal ? await GetMostActiveContributor(org, repoName) : null;
                        var prCount = await _githubApi.GetPullRequestCount(org, repoName);

                        result.Append($"\"{org}\",\"{repoName}\",\"{url}\",\"{repoVisibility}\",\"{lastCommitDate:dd-MMM-yyyy hh:mm tt}\",\"{repoSize:N0}\",{prCount},{commitCount}");
                        result.AppendLine(!minimal ? $",\"{mostActiveContributor}\"" : null);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    _log.LogError(ex);
                    _log.LogWarning($"Error occurred while processing repo {repoName}, will skip this repo and continue");
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            return result.ToString();
        }

        // TODO: reduce duplication where I copied this code from
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

        private async Task<string> GetMostActiveContributor(string org, string repo)
        {
            var authors = await _githubApi.GetAuthorsSince(org, repo, DateTime.Today.AddYears(-1));
            authors = authors.Where(x => !x.Contains("[bot]"));
            var mostActiveContributor = authors.Any() ? authors.GroupBy(x => x)
                                                               .OrderByDescending(x => x.Count())
                                                               .First()
                                                               .First()
                                                      : "N/A";

            return mostActiveContributor;
        }
    }
}
