using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.TestPipelines
{
    public class TestPipelinesCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        public string TargetApiUrl { get; set; }
        public int MonitorTimeoutMinutes { get; set; } = 30;
        public string PipelineFilter { get; set; }
        public int MaxConcurrentTests { get; set; } = 3;
        public string ReportPath { get; set; } = "pipeline-test-report.json";
    }
}
