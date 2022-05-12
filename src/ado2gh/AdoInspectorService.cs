using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub
{
    public class AdoInspectorService
    {
        private readonly OctoLogger _log;

        public AdoInspectorService(OctoLogger log)
        {
            _log = log;
        }

        public virtual async Task<IEnumerable<string>> GetOrgs(AdoApi api, string org = null)
        {
            var orgs = new List<string>();

            if (org.HasValue())
            {
                orgs.Add(org);
            }
            else
            {
                if (api != null)
                {
                    _log.LogInformation($"Retrieving list of all Orgs PAT has access to...");
                    var userId = await api.GetUserId();
                    orgs = (await api.GetOrganizations(userId)).ToList();
                }
            }

            return orgs;
        }

        public virtual async Task<IDictionary<string, IEnumerable<string>>> GetTeamProjects(AdoApi api, IEnumerable<string> orgs, string teamProject = null)
        {
            var teamProjects = new Dictionary<string, IEnumerable<string>>();

            if (teamProject.HasValue())
            {
                if (orgs.Count() != 1)
                {
                    throw new ArgumentException($"{nameof(orgs)} must contain exactly one item when passing teamProject", nameof(orgs));
                }

                teamProjects.Add(orgs.First(), new List<string>() { teamProject });

                return teamProjects;
            }

            if (api != null && orgs != null)
            {
                foreach (var org in orgs)
                {
                    var projects = await api.GetTeamProjects(org);
                    teamProjects.Add(org, projects);
                }
            }

            return teamProjects;
        }

        public virtual async Task<IDictionary<string, IDictionary<string, IEnumerable<string>>>> GetRepos(AdoApi api, IDictionary<string, IEnumerable<string>> teamProjects, string repo = null)
        {
            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();

            if (repo.HasValue())
            {
                if (teamProjects is null || teamProjects.Count != 1 || teamProjects.First().Value.Count() != 1)
                {
                    throw new ArgumentException($"{nameof(teamProjects)} must contain exactly one org and one team project when passing {nameof(repo)}", nameof(teamProjects));
                }
                var teamProjectRepos = new Dictionary<string, IEnumerable<string>>
                {
                    { teamProjects.First().Value.First(), new List<string>() { repo } }
                };

                repos.Add(teamProjects.First().Key, teamProjectRepos);

                return repos;
            }

            if (api != null && teamProjects != null)
            {
                foreach (var org in teamProjects.Keys)
                {
                    repos.Add(org, new Dictionary<string, IEnumerable<string>>());

                    foreach (var teamProject in teamProjects[org])
                    {
                        var teamProjectRepos = await api.GetEnabledRepos(org, teamProject);
                        repos[org].Add(teamProject, teamProjectRepos);
                    }
                }
            }

            return repos;
        }

        public virtual async Task<IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>> GetPipelines(AdoApi api, IDictionary<string, IDictionary<string, IEnumerable<string>>> repos)
        {
            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>();

            if (api != null && repos != null)
            {
                foreach (var org in repos.Keys)
                {
                    pipelines.Add(org, new Dictionary<string, IDictionary<string, IEnumerable<string>>>());

                    foreach (var teamProject in repos[org].Keys)
                    {
                        pipelines[org].Add(teamProject, new Dictionary<string, IEnumerable<string>>());

                        foreach (var repo in repos[org][teamProject])
                        {
                            var repoId = await api.GetRepoId(org, teamProject, repo);
                            var repoPipelines = await api.GetPipelines(org, teamProject, repoId);
                            pipelines[org][teamProject].Add(repo, repoPipelines);
                        }
                    }
                }
            }

            return pipelines;
        }
    }
}
