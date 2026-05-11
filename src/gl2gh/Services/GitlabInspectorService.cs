using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub
{
    public class GitlabInspectorService
    {
        private readonly OctoLogger _log;
        private readonly GitlabApi _gitlabApi;

        private IList<(string, string)> _groups;
        private readonly IDictionary<string, IList<GitlabProject>> _repos = new Dictionary<string, IList<GitlabProject>>();
        private readonly IDictionary<string, IDictionary<string, int>> _mrCounts = new Dictionary<string, IDictionary<string, int>>();

        public GitlabInspectorService(OctoLogger log, GitlabApi gitlabApi)
        {
            _log = log;
            _gitlabApi = gitlabApi;
        }

        public virtual async Task<IEnumerable<(string Path, string Name)>> GetGroups()
        {
            if (_groups is null)
            {
                _log.LogInformation($"Retrieving list of all Groups the user has access to...");
                _groups = (await _gitlabApi.GetGroups())
                    .Select(group => (group.Path, group.Name))
                    .ToList();
            }

            return _groups;
        }

        public virtual async Task<(string Key, string Name)> GetGroup(string groupPath)
        {
            _log.LogInformation($"Retrieving Group...");
            var (_, Key, Name) = await _gitlabApi.GetGroup(groupPath);

            return (Key, Name);
        }

        public virtual async Task<IEnumerable<GitlabProject>> GetProjects(string groupPath)
        {
            if (!_repos.TryGetValue(groupPath, out var repos))
            {
                repos = (await _gitlabApi.GetProjects(groupPath))
                    .Select(repo => new GitlabProject() { Name = repo.Name, Path = repo.Path })
                    .ToList();
                _repos.Add(groupPath, repos);
            }

            return repos;
        }

        public virtual async Task<int> GetProjectCount(string[] groups)
        {
            return await groups.Sum(async key => await GetProjectCount(key));
        }

        public virtual async Task<int> GetProjectCount()
        {
            var groups = await GetGroups();
            return await groups.Sum(async group => await GetProjectCount(group.Path));
        }

        public virtual async Task<int> GetProjectCount(string groupPath)
        {
            return (await GetProjects(groupPath)).Count();
        }

        public virtual async Task<int> GetMergeRequestCount(string groupPath)
        {
            var repos = await GetProjects(groupPath);
            return await repos.Sum(async repo => await GetProjectMergeRequestCount(groupPath, repo.Name));
        }

        public virtual async Task<int> GetProjectMergeRequestCount(string groupPath, string repo)
        {
            if (!_mrCounts.ContainsKey(groupPath))
            {
                _mrCounts.Add(groupPath, new Dictionary<string, int>());
            }

            if (!_mrCounts[groupPath].TryGetValue(repo, out var mrCount))
            {
                mrCount = await _gitlabApi.GetMergeRequestCount(groupPath, repo);
                _mrCounts[groupPath][repo] = mrCount;
            }

            return mrCount;
        }
    }
}
