namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateCodeScanningAlerts;

public class MigrateCodeScanningAlertsCommandArgs
{
    public string SourceOrg { get; set; }
    public string SourceRepo { get; set; }
    public string TargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GhesApiUrl { get; set; }
    public bool NoSslVerify { get; set; }
    public bool Verbose { get; set; }
    public bool DryRun { get; set; }
    public string GithubSourcePat { get; set; }
    public string GithubTargetPat { get; set; }
}
