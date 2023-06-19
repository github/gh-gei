using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub
{
    public class BbsInspectorService
    {
        private readonly OctoLogger _log;
        private readonly BbsApi _bbsApi;

        private IList<string> _projects;
        private readonly IDictionary<string, IList<BbsRepository>> _repos = new Dictionary<string, IList<BbsRepository>>();
        private readonly IDictionary<string, IDictionary<string, int>> _prCounts = new Dictionary<string, IDictionary<string, int>>();

        public BbsInspectorService(OctoLogger log, BbsApi bbsApi)
        {
            _log = log;
            _bbsApi = bbsApi;
        }

        public virtual async Task<IEnumerable<string>> GetProjects()
        {
            if (_projects is null)
            {
                _log.LogInformation($"Retrieving list of all Projects the user has access to...");
                _projects = (await _bbsApi.GetProjects())
                    .Select(project => project.Key)
                    .ToList();
            }

            return _projects;
        }

        public virtual async Task<IEnumerable<BbsRepository>> GetRepos(string project)
        {
            if (!_repos.TryGetValue(project, out var repos))
            {
                repos = (await _bbsApi.GetRepos(project))
                    .Select(repo => new BbsRepository() { Name = repo.Name, Slug = repo.Slug })
                    .ToList();
                _repos.Add(project, repos);
            }

            return repos;
        }

        public virtual async Task<int> GetRepoCount(string[] projects)
        {
            return await projects.Sum(async key => await GetRepoCount(key));
        }

        public virtual async Task<int> GetRepoCount()
        {
            var projects = await GetProjects();
            return await projects.Sum(async key => await GetRepoCount(key));
        }

        public virtual async Task<int> GetRepoCount(string project)
        {
            return (await GetRepos(project)).Count();
        }

        public virtual async Task<int> GetPullRequestCount(string project)
        {
            var repos = await GetRepos(project);
            return await repos.Sum(async repo => await GetRepositoryPullRequestCount(project, repo.Name));
        }

        public virtual async Task<int> GetRepositoryPullRequestCount(string project, string repo)
        {
            if (!_prCounts.ContainsKey(project))
            {
                _prCounts.Add(project, new Dictionary<string, int>());
            }

            if (!_prCounts[project].TryGetValue(repo, out var prCount))
            {
                prCount = (await _bbsApi.GetRepositoryPullRequests(project, repo)).Count();
                _prCounts[project][repo] = prCount;
            }

            return prCount;
        }

        public virtual async Task<DateTime> GetLastCommitDate(string project, string repo)
        {
            var commit = await _bbsApi.GetRepositoryLatestCommit(project, repo);
            var authorTimestamp = 0L;

            if (commit?["values"] != null && commit["values"].Any())
            {
                authorTimestamp = (long)commit["values"][0]["authorTimestamp"];
            }

            var dateTime = authorTimestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(authorTimestamp).DateTime : DateTime.MinValue;

            return dateTime.Date;
        }

        public virtual async Task<(ulong repoSize, ulong attachmentsSize)> GetRepositoryAndAttachmentsSize(string project, string repo, string bbsUsername, string bbsPassword)
        {
            var sizes = await _bbsApi.GetRepositorySize(project, repo, bbsUsername, bbsPassword);

            var repoSize = (ulong)sizes["repository"];
            var attachmentsSize = (ulong)sizes["attachments"];

            return (repoSize, attachmentsSize);
        }
    }
}
