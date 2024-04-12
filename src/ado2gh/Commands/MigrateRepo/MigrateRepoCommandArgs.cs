using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string AdoServerUrl { get; set; }
        public bool QueueOnly { get; set; }
        public string TargetRepoVisibility { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        [Secret]
        public string GithubPat { get; set; }
        public string TargetApiUrl { get; set; }
    }
}
