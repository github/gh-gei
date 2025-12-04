using OctoshiftCLI.Commands;

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
    }
}
