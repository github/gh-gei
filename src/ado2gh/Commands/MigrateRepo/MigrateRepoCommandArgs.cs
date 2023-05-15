namespace OctoshiftCLI.AdoToGithub.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string AdoServerUrl { get; set; }
        public bool Wait { get; set; }
        public bool QueueOnly { get; set; }
        public string TargetRepoVisibility { get; set; }
        public string AdoPat { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
