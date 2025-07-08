using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub
{
    public class AdoInspectorService
    {
        private readonly OctoLogger _log;
        private readonly AdoApi _adoApi;

        internal Func<string, Stream> OpenFileStream = path => File.OpenRead(path);

        private IList<string> _orgs;
        private readonly IDictionary<string, IList<string>> _teamProjects = new Dictionary<string, IList<string>>();
        private readonly IDictionary<string, IDictionary<string, IList<AdoRepository>>> _repos = new Dictionary<string, IDictionary<string, IList<AdoRepository>>>();
        private readonly IDictionary<string, IDictionary<string, IDictionary<string, IList<string>>>> _pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IList<string>>>>();
        private readonly IDictionary<string, IDictionary<string, IDictionary<string, int>>> _prCounts = new Dictionary<string, IDictionary<string, IDictionary<string, int>>>();

        public AdoInspectorService(OctoLogger log, AdoApi adoApi)
        {
            _log = log;
            _adoApi = adoApi;
        }

        public string OrgFilter { get; set; }
        public string TeamProjectFilter { get; set; }
        public string RepoFilter { get; set; }

        public virtual void LoadReposCsv(string csvPath)
        {
            _orgs = [];

            using var csvStream = OpenFileStream(csvPath);
            using var csvParser = new TextFieldParser(csvStream);
            csvParser.SetDelimiters(",");
            csvParser.ReadFields(); // skip the header row

            while (!csvParser.EndOfData)
            {
                var fields = csvParser.ReadFields();

                var org = fields[0];
                var teamProject = fields[1];
                var repo = fields[2];

                if (!_orgs.Any(x => x == org))
                {
                    _orgs.Add(org);
                }

                if (!_teamProjects.ContainsKey(org))
                {
                    _teamProjects.Add(org, []);
                }

                if (!_repos.ContainsKey(org))
                {
                    _repos.Add(org, new Dictionary<string, IList<AdoRepository>>());
                }

                if (!_teamProjects[org].Any(x => x == teamProject))
                {
                    _teamProjects[org].Add(teamProject);
                }

                if (!_repos[org].ContainsKey(teamProject))
                {
                    _repos[org].Add(teamProject, []);
                }

                if (!_repos[org][teamProject].Any(x => x.Name == repo))
                {
                    _repos[org][teamProject].Add(new AdoRepository() { Name = repo });
                }
            }
        }

        public virtual async Task<IEnumerable<string>> GetOrgs()
        {
            if (_orgs is null)
            {
                if (OrgFilter.HasValue())
                {
                    _orgs = [OrgFilter];
                }
                else
                {
                    _log.LogInformation($"Retrieving list of all Orgs PAT has access to...");
                    var userId = await _adoApi.GetUserId();
                    _orgs = (await _adoApi.GetOrganizations(userId)).ToList();
                }
            }

            return _orgs;
        }

        public virtual async Task<int> GetRepoCount()
        {
            var orgs = await GetOrgs();
            return await orgs.Sum(async org => await GetRepoCount(org));
        }

        public virtual async Task<int> GetRepoCount(string org)
        {
            var teamProjects = await GetTeamProjects(org);
            return await teamProjects.Sum(async tp => await GetRepoCount(org, tp));
        }

        public virtual async Task<int> GetRepoCount(string org, string teamProject)
        {
            return (await GetRepos(org, teamProject)).Count();
        }

        public virtual async Task<int> GetTeamProjectCount(string org)
        {
            return (await GetTeamProjects(org)).Count();
        }

        public virtual async Task<int> GetTeamProjectCount()
        {
            var orgs = await GetOrgs();
            return await orgs.Sum(async o => await GetTeamProjectCount(o));
        }

        public virtual async Task<int> GetPipelineCount()
        {
            var orgs = await GetOrgs();
            return await orgs.Sum(async o => await GetPipelineCount(o));
        }

        public virtual async Task<int> GetPipelineCount(string org)
        {
            var teamProjects = await GetTeamProjects(org);
            return await teamProjects.Sum(async tp => await GetPipelineCount(org, tp));
        }

        public virtual async Task<int> GetPipelineCount(string org, string teamProject)
        {
            var repos = await GetRepos(org, teamProject);
            return await repos.Sum(async r => await GetPipelineCount(org, teamProject, r.Name));
        }

        public virtual async Task<int> GetPipelineCount(string org, string teamProject, string repo)
        {
            return (await GetPipelines(org, teamProject, repo)).Count();
        }

        public virtual async Task<int> GetPullRequestCount(string org, string teamProject)
        {
            var repos = await GetRepos(org, teamProject);
            return await repos.Sum(async r => await GetPullRequestCount(org, teamProject, r.Name));
        }

        public virtual async Task<int> GetPullRequestCount(string org)
        {
            var teamProjects = await GetTeamProjects(org);
            return await teamProjects.Sum(async tp => await GetPullRequestCount(org, tp));
        }

        public virtual async Task<IEnumerable<string>> GetTeamProjects(string org)
        {
            if (!_teamProjects.TryGetValue(org, out var teamProjects))
            {
                teamProjects = TeamProjectFilter.HasValue() ? [TeamProjectFilter] : (await _adoApi.GetTeamProjects(org)).ToList();
                _teamProjects.Add(org, teamProjects);
            }

            return teamProjects;
        }

        public virtual async Task<IEnumerable<AdoRepository>> GetRepos(string org, string teamProject)
        {
            if (!_repos.ContainsKey(org))
            {
                _repos.Add(org, new Dictionary<string, IList<AdoRepository>>());
            }

            if (!_repos[org].TryGetValue(teamProject, out var repos))
            {
                repos = (await _adoApi.GetEnabledRepos(org, teamProject)).ToList();
                _repos[org].Add(teamProject, repos);
            }

            return repos;
        }

        public virtual async Task<IEnumerable<string>> GetPipelines(string org, string teamProject, string repo)
        {
            if (!_pipelines.ContainsKey(org))
            {
                _pipelines.Add(org, new Dictionary<string, IDictionary<string, IList<string>>>());
            }

            if (!_pipelines[org].ContainsKey(teamProject))
            {
                _pipelines[org].Add(teamProject, new Dictionary<string, IList<string>>());
            }

            if (!_pipelines[org][teamProject].TryGetValue(repo, out var pipelines))
            {
                await _adoApi.PopulateRepoIdCache(org, teamProject);
                var repoId = await _adoApi.GetRepoId(org, teamProject, repo);
                pipelines = (await _adoApi.GetPipelines(org, teamProject, repoId)).ToList();

                _pipelines[org][teamProject].Add(repo, pipelines);
            }

            return pipelines;
        }

        public virtual async Task<int> GetPullRequestCount(string org, string teamProject, string repo)
        {
            if (!_prCounts.ContainsKey(org))
            {
                _prCounts.Add(org, new Dictionary<string, IDictionary<string, int>>());
            }

            if (!_prCounts[org].ContainsKey(teamProject))
            {
                _prCounts[org].Add(teamProject, new Dictionary<string, int>());
            }

            if (!_prCounts[org][teamProject].TryGetValue(repo, out var prCount))
            {
                prCount = await _adoApi.GetPullRequestCount(org, teamProject, repo);
                _prCounts[org][teamProject][repo] = prCount;
            }

            return prCount;
        }

        public virtual void OutputRepoListToLog()
        {
            foreach (var org in _repos.Keys)
            {
                _log.LogInformation($"ADO Org: {org}");

                foreach (var teamProject in _repos[org].Keys)
                {
                    _log.LogInformation($"  Team Project: {teamProject}");

                    foreach (var repo in _repos[org][teamProject])
                    {
                        _log.LogInformation($"    Repo: {repo.Name}");
                    }
                }
            }
        }
    }
}
