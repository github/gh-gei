namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class MigrateOrgCommandArgs
{
    public string GithubSourceOrg { get; set; }
    public string GithubTargetOrg { get; set; }
    public string GithubTargetEnterprise { get; set; }
    public bool Wait { get; set; }
    public bool Verbose { get; set; }
    public string GithubSourcePat { get; set; }
    public string GithubTargetPat { get; set; }
}
