namespace OctoshiftCLI.AdoToGithub.Commands;

public class ShareServiceConnectionCommandArgs
{
    public string AdoOrg { get; set; }
    public string AdoTeamProject { get; set; }
    public string ServiceConnectionId { get; set; }
    public string AdoPat { get; set; }
    public bool Verbose { get; set; }
}
