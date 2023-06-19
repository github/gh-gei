﻿using System;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Factories;

namespace OctoshiftCLI.BbsToGithub
{
    public class ProjectsCsvGeneratorService
    {
        private readonly BbsInspectorServiceFactory _bbsInspectorServiceFactory;
        private readonly BbsApiFactory _bbsApiFactory;

        public ProjectsCsvGeneratorService(BbsInspectorServiceFactory bbsInspectorServiceFactory, BbsApiFactory bbsApiFactory)
        {
            _bbsInspectorServiceFactory = bbsInspectorServiceFactory;
            _bbsApiFactory = bbsApiFactory;
        }

        public virtual async Task<string> Generate(string bbsServerUrl, string bbsUsername, string bbsPassword, bool noSslVerify, string bbsProject = "", bool minimal = false)
        {
            bbsServerUrl = bbsServerUrl ?? throw new ArgumentNullException(nameof(bbsServerUrl));
            bbsUsername = bbsUsername ?? throw new ArgumentNullException(nameof(bbsUsername));
            bbsPassword = bbsPassword ?? throw new ArgumentNullException(nameof(bbsPassword));

            var bbsApi = _bbsApiFactory.Create(bbsServerUrl, bbsUsername, bbsPassword, noSslVerify);
            var inspector = _bbsInspectorServiceFactory.Create(bbsApi);
            var result = new StringBuilder();

            result.Append("name,url,repo-count");
            result.AppendLine(!minimal ? ",pr-count" : null);

            var projects = string.IsNullOrWhiteSpace(bbsProject) ? await inspector.GetProjects() : new[] { await inspector.GetProject(bbsProject) };

            foreach (var (Key, Name) in projects)
            {
                var url = $"{bbsServerUrl.TrimEnd('/')}/projects/{Key}";
                var repoCount = await inspector.GetRepoCount(Key);
                var prCount = !minimal ? await inspector.GetPullRequestCount(Key) : 0;

                result.Append($"\"{Name}\",\"{url}\",{repoCount}");
                result.AppendLine(!minimal ? $",{prCount}" : null);
            }

            return result.ToString();
        }
    }
}