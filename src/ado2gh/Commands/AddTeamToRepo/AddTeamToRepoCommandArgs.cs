using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.AddTeamToRepo
{
    public class AddTeamToRepoCommandArgs : CommandArgs
    {
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string Team { get; set; }
        public string Role { get; set; }
        [Secret]
        public string GithubPat { get; set; }
        public string TargetApiUrl { get; set; }
    }
}
