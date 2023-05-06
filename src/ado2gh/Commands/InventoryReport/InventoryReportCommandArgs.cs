using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.InventoryReport
{
    public class InventoryReportCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        public bool Minimal { get; set; }
    }
}
