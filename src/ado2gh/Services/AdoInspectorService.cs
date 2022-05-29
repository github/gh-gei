using System.Collections.Generic;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub
{
    public class AdoInspectorService
    {
        private readonly OctoLogger _log;
        public AdoApi AdoApi { get; set; }

        private IEnumerable<string> _orgs;
        private readonly IDictionary<string, IEnumerable<string>> _teamProjects = new Dictionary<string, IEnumerable<string>>();
        private readonly IDictionary<string, IDictionary<string, IEnumerable<string>>> _repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();
        private readonly IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> _pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>();
        private readonly IDictionary<string, IDictionary<string, IDictionary<string, int>>> _prCounts = new Dictionary<string, IDictionary<string, IDictionary<string, int>>>();

        public AdoInspectorService(OctoLogger log)
        {
            _log = log;
        }

        public string OrgFilter { get; set; }
        public string TeamProjectFilter { get; set; }
        public string RepoFilter { get; set; }

        public virtual async Task<IEnumerable<string>> GetOrgs()
        {
            if (_orgs is null)
            {
                if (OrgFilter.HasValue())
                {
                    _orgs = new List<string>() { OrgFilter };
                }
                else
                {
                    _log.LogInformation($"Retrieving list of all Orgs PAT has access to...");
                    var userId = await AdoApi.GetUserId();
                    _orgs = await AdoApi.GetOrganizations(userId);
                }
            }

            return _orgs;
        }

        public virtual async Task<IEnumerable<string>> GetTeamProjects(string org)
        {
            if (!_teamProjects.TryGetValue(org, out var teamProjects))
            {
                teamProjects = TeamProjectFilter.HasValue() ? new List<string>() { TeamProjectFilter } : await AdoApi.GetTeamProjects(org);
                _teamProjects.Add(org, teamProjects);
            }

            return teamProjects;
        }

        public virtual async Task<IEnumerable<string>> GetRepos(string org, string teamProject)
        {
            if (!_repos.ContainsKey(org))
            {
                _repos.Add(org, new Dictionary<string, IEnumerable<string>>());
            }

            if (!_repos[org].TryGetValue(teamProject, out var repos))
            {
                repos = await AdoApi.GetEnabledRepos(org, teamProject);
                _repos[org].Add(teamProject, repos);
            }

            return repos;
        }

        public virtual async Task<IEnumerable<string>> GetPipelines(string org, string teamProject, string repo)
        {
            if (!_pipelines.ContainsKey(org))
            {
                _pipelines.Add(org, new Dictionary<string, IDictionary<string, IEnumerable<string>>>());
            }

            if (!_pipelines[org].ContainsKey(teamProject))
            {
                _pipelines[org].Add(teamProject, new Dictionary<string, IEnumerable<string>>());
            }

            if (!_pipelines[org][teamProject].TryGetValue(repo, out var pipelines))
            {
                await AdoApi.PopulateRepoIdCache(org, teamProject);
                var repoId = await AdoApi.GetRepoId(org, teamProject, repo);
                pipelines = await AdoApi.GetPipelines(org, teamProject, repoId);

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
                prCount = await AdoApi.GetPullRequestCount(org, teamProject, repo);
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
                        _log.LogInformation($"    Repo: {repo}");
                    }
                }
            }
        }
    }
}
