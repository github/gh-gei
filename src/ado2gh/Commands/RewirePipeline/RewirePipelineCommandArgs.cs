using System;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.RewirePipeline
{
    public class RewirePipelineCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoPipeline { get; set; }
        public int? AdoPipelineId { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        public string TargetApiUrl { get; set; }
        public bool DryRun { get; set; }
        public int MonitorTimeoutMinutes { get; set; } = 30;

        public override void Log(OctoLogger log)
        {
            if (log is null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            log.Verbose = Verbose;

            // Log all properties except MonitorTimeoutMinutes
            log.LogInformation($"ADO ORG: {AdoOrg}");
            log.LogInformation($"ADO TEAM PROJECT: {AdoTeamProject}");

            if (AdoPipeline.HasValue())
            {
                log.LogInformation($"ADO PIPELINE: {AdoPipeline}");
            }

            if (AdoPipelineId.HasValue)
            {
                log.LogInformation($"ADO PIPELINE ID: {AdoPipelineId}");
            }

            log.LogInformation($"GITHUB ORG: {GithubOrg}");
            log.LogInformation($"GITHUB REPO: {GithubRepo}");
            log.LogInformation($"SERVICE CONNECTION ID: {ServiceConnectionId}");

            if (TargetApiUrl.HasValue())
            {
                log.LogInformation($"TARGET API URL: {TargetApiUrl}");
            }

            if (DryRun)
            {
                log.LogInformation($"DRY RUN: true");
            }
        }
    }
}
