using OctoshiftCLI.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateCodeScanningAlerts;

public class MigrateCodeScanningAlertsCommandArgs : CommandArgs
{
    public string SourceOrg { get; set; }
    public string SourceRepo { get; set; }
    public string TargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GhesApiUrl { get; set; }
    public bool NoSslVerify { get; set; }
    public bool DryRun { get; set; }
    public string GithubSourcePat { get; set; }
    public string GithubTargetPat { get; set; }
}
