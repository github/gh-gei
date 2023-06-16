using OctoshiftCLI.Commands;

namespace OctoshiftCLI.BbsToGithub.Commands.InventoryReport
{
    public class InventoryReportCommandArgs : CommandArgs
    {
        public string BbsServerUrl { get; set; }
        public string BbsProject { get; set; }
        public string BbsUsername { get; set; }
        [Secret]
        public string BbsPassword { get; set; }
        public bool NoSslVerify { get; set; }
        public bool Minimal { get; set; }
    }
}
