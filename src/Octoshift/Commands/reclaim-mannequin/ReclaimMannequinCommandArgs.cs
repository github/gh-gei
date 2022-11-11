namespace OctoshiftCLI.Commands;

public class ReclaimMannequinCommandArgs
{
    public string GithubOrg { get; set; }
    public string Csv { get; set; }
    public string MannequinUser { get; set; }
    public string MannequinId { get; set; }
    public string TargetUser { get; set; }
    public bool Force { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
