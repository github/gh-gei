namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class MigrateOrgCommandArgs : CommandArgs
{
    public string GithubSourceOrg { get; set; }
    public string GithubTargetOrg { get; set; }
    public string GithubTargetEnterprise { get; set; }
    public bool Wait { get; set; }
    [Secret]
    public string GithubSourcePat { get; set; }
    [Secret]
    public string GithubTargetPat { get; set; }
}
