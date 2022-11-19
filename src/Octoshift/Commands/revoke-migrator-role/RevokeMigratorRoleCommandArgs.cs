namespace OctoshiftCLI.Commands;

public class RevokeMigratorRoleCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string GhesApiUrl { get; set; }
}
