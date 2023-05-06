using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.ShareServiceConnection
{
    public class ShareServiceConnectionCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string ServiceConnectionId { get; set; }
        [Secret]
        public string AdoPat { get; set; }
    }
}
