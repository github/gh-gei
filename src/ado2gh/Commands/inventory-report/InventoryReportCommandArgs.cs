namespace OctoshiftCLI.AdoToGithub.Commands;

public class InventoryReportCommandArgs
{
    public string AdoOrg { get; set; }
    public string AdoPat { get; set; }
    public bool Minimal { get; set; }
    public bool Verbose { get; set; }
}
