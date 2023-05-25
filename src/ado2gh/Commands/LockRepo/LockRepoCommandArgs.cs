using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.LockRepo
{
    public class LockRepoCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        [Secret]
        public string AdoPat { get; set; }
    }
}
