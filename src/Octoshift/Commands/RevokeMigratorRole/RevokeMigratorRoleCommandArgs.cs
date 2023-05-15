namespace OctoshiftCLI.Commands.RevokeMigratorRole;

public class RevokeMigratorRoleCommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
    public string GhesApiUrl { get; set; }
}
