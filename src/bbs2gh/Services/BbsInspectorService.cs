using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub
{
    public class BbsInspectorService
    {
        private readonly OctoLogger _log;
        private readonly BbsApi _bbsApi;

        internal Func<string, Stream> OpenFileStream = path => File.OpenRead(path);

        private IList<string> _projects;
        private readonly IDictionary<string, IList<BbsRepository>> _repos = new Dictionary<string, IList<BbsRepository>>();
        private readonly IDictionary<string, IDictionary<string, int>> _prCounts = new Dictionary<string, IDictionary<string, int>>();

        public BbsInspectorService(OctoLogger log, BbsApi bbsApi)
        {
            _log = log;
            _bbsApi = bbsApi;
        }

        public string ProjectFilter { get; set; }
        public string RepoFilter { get; set; }

        public virtual void LoadReposCsv(string csvPath)
        {
            _projects = new List<string>();

            using var csvStream = OpenFileStream(csvPath);
            using var csvParser = new TextFieldParser(csvStream);
            csvParser.SetDelimiters(",");
            csvParser.ReadFields(); // skip the header row

            while (!csvParser.EndOfData)
            {
                var fields = csvParser.ReadFields();

                var project = fields[0];
                var repo = fields[1];

                if (!_projects.Any(x => x == project))
                {
                    _projects.Add(project);
                }

                if (!_repos.ContainsKey(project))
                {
                    _repos.Add(project, new List<BbsRepository>());
                }

                if (!_repos[project].Any(x => x.Name == repo))
                {
                    _repos[project].Add(new BbsRepository() { Name = repo });
                }
            }
        }

        public virtual async Task<IEnumerable<string>> GetProjects()
        {
            if (_projects is null)
            {
                if (ProjectFilter.HasValue())
                {
                    _projects = new List<string>() { ProjectFilter };
                }
                else
                {
                    _log.LogInformation($"Retrieving list of all Projects the user has access to...");
                    _projects = (await _bbsApi.GetProjects())
                        .Select(project => project.Name)
                        .ToList();
                }
            }

            return _projects;
        }

        public virtual async Task<IEnumerable<BbsRepository>> GetRepos(string project)
        {
            if (!_repos.TryGetValue(project, out var repos))
            {
                repos = (await _bbsApi.GetRepos(project))
                    .Select(repo => (new BbsRepository() { Name = repo.Name, Archived = repo.Archived }))
                    .ToList();
                _repos.Add(project, repos);
            }

            return repos;
        }

        public virtual async Task<int> GetRepoCount()
        {
            var projects = await GetProjects();
            return await projects.Sum(async project => await GetRepoCount(project));
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

            var authorTimestamp = commit["values"].Any() ? (long)commit["values"][0]["authorTimestamp"] : 0;

            var dateTime = authorTimestamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(authorTimestamp).DateTime : DateTime.MinValue;

            return dateTime.Date;
        }

        public virtual void OutputRepoListToLog()
        {
            foreach (var project in _repos.Keys)
            {
                _log.LogInformation($"BBS Project: {project}");

                foreach (var repo in _repos[project])
                {
                    _log.LogInformation($"    Repo: {repo.Name}");
                }
            }
        }
    }
}
