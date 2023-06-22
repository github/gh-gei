using OctoshiftCLI.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport
{
    public class InventoryReportCommandArgs : CommandArgs
    {
        public string GithubOrg { get; set; }
        [Secret]
        public string GithubPat { get; set; }
        public string GhesApiUrl { get; set; }
        public bool NoSslVerify { get; set; }
        public bool Minimal { get; set; }
    }
}
