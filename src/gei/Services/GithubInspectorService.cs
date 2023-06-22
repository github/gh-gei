using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Services
{
    public class GithubInspectorService
    {
        private readonly OctoLogger _log;
        private readonly GithubApi _githubApi;
        private readonly FileSystemProvider _fileSystemProvider;

        private readonly IDictionary<string, IList<(string Name, string Visibility, long Size)>> _repos = new Dictionary<string, IList<(string Name, string Visibility, long Size)>>();
        private readonly IDictionary<string, int> _prCounts = new Dictionary<string, int>();

        public GithubInspectorService(OctoLogger log, FileSystemProvider fileSystemProvider, GithubApi githubApi)
        {
            _log = log;
            _fileSystemProvider = fileSystemProvider;
            _githubApi = githubApi;
        }

        public virtual void LoadReposCsv(string csvPath)
        {
            using var csvStream = _fileSystemProvider.OpenRead(csvPath);
            using var csvParser = new TextFieldParser(csvStream);
            csvParser.SetDelimiters(",");
            csvParser.ReadFields(); // skip the header row

            while (!csvParser.EndOfData)
            {
                var fields = csvParser.ReadFields();

                var org = fields[0];
                var repo = fields[1];
                var visibility = fields[2];
                var size = int.Parse(fields[5]);

                if (!_repos.ContainsKey(org))
                {
                    _repos.Add(org, new List<(string Name, string Visibility, long Size)>());
                }

                _repos[org].Add((repo, visibility, size));
            }
        }

        public virtual async Task<int> GetRepoCount(string org)
        {
            return (await GetRepos(org)).Count();
        }

        public virtual async Task<int> GetPullRequestCount(string org)
        {
            var repos = await GetRepos(org);
            return await repos.Sum(async repo => await GetPullRequestCount(org, repo.Name));
        }

        public virtual async Task<IEnumerable<(string Name, string Visibility, long Size)>> GetRepos(string org)
        {
            if (!_repos.ContainsKey(org))
            {
                _repos.Add(org, (await _githubApi.GetRepos(org)).ToList());
            }

            return _repos[org];
        }

        public virtual async Task<int> GetPullRequestCount(string org, string repo)
        {
            if (!_prCounts.TryGetValue(repo, out var prCount))
            {
                prCount = await _githubApi.GetPullRequestCount(org, repo);
                _prCounts[repo] = prCount;
            }

            return prCount;
        }

        public virtual void OutputRepoListToLog()
        {
            foreach (var repo in _repos)
            {
                _log.LogInformation($"Repo: {repo}");
            }
        }
    }
}
