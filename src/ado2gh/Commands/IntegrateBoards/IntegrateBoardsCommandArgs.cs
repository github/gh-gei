namespace OctoshiftCLI.AdoToGithub.Commands.IntegrateBoards
{
    public class IntegrateBoardsCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string AdoPat { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
