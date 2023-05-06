using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.DisableRepo
{
    public class DisableRepoCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        [Secret]
        public string AdoPat { get; set; }
    }
}
