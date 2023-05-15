namespace OctoshiftCLI.AdoToGithub.Commands.RewirePipeline
{
    public class RewirePipelineCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoPipeline { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
