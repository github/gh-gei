namespace OctoshiftCLI.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public string Csv { get; set; }
    public string MannequinUser { get; set; }
    public string MannequinId { get; set; }
    public string TargetUser { get; set; }
    public bool Force { get; set; }
    [Secret]
    public string GithubPat { get; set; }
}
