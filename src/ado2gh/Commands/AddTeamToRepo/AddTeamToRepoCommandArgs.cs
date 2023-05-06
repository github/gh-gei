using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.AddTeamToRepo
{
    public class AddTeamToRepoCommandArgs : CommandArgs
    {
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string Team { get; set; }
        public string Role { get; set; }
        public string GithubPat { get; set; }
    }
}
