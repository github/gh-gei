using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.IntegrateBoards
{
    public class IntegrateBoardsCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        [Secret]
        public string AdoPat { get; set; }
    }
}
