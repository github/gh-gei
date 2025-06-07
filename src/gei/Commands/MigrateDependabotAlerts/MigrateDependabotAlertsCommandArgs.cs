using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateDependabotAlerts;

public class MigrateDependabotAlertsCommandArgs : CommandArgs
{
    public string SourceOrg { get; set; }
    public string SourceRepo { get; set; }
    public string TargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GhesApiUrl { get; set; }
    public bool NoSslVerify { get; set; }
    public bool DryRun { get; set; }
    [Secret]
    public string GithubSourcePat { get; set; }
    [Secret]
    public string GithubTargetPat { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (SourceRepo.HasValue() && TargetRepo.IsNullOrWhiteSpace())
        {
            TargetRepo = SourceRepo;
            log?.LogInformation("Since target-repo is not provided, source-repo value will be used for target-repo.");
        }
    }
}