using OctoshiftCLI.Commands;

namespace OctoshiftCLI.GitlabToGithub.Commands.InventoryReport
{
    public class InventoryReportCommandArgs : CommandArgs
    {
        public string GitlabServerUrl { get; set; }
        public string GitlabProject { get; set; }
        public string GitlabUsername { get; set; }
        [Secret]
        public string GitlabPassword { get; set; }
        public bool NoSslVerify { get; set; }
        public bool Minimal { get; set; }
    }
}
