using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateCodeScanningAlerts;

public class MigrateCodeScanningAlertsCommandArgs : CommandArgs
{
    public string SourceOrg { get; set; }
    public string SourceRepo { get; set; }
    public string TargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GhesApiUrl { get; set; }
    public string GithubSourceApiUrl { get; set; }
    public bool NoSslVerify { get; set; }
    public bool DryRun { get; set; }
    [Secret]
    public string GithubSourcePat { get; set; }
    [Secret]
    public string GithubTargetPat { get; set; }

    public string GetSourceApiUrl() => GithubSourceApiUrl.HasValue() ? GithubSourceApiUrl : GhesApiUrl;

    public override void Validate(OctoLogger log)
    {
        if (SourceOrg.IsUrl())
        {
            throw new OctoshiftCliException($"The --source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        if (TargetOrg.IsUrl())
        {
            throw new OctoshiftCliException($"The --target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        if (SourceRepo.IsUrl())
        {
            throw new OctoshiftCliException($"The --source-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
        }

        if (TargetRepo.IsUrl())
        {
            throw new OctoshiftCliException($"The --target-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
        }

        if (SourceRepo.HasValue() && TargetRepo.IsNullOrWhiteSpace())
        {
            TargetRepo = SourceRepo;
            log?.LogInformation("Since target-repo is not provided, source-repo value will be used for target-repo.");
        }

        if (GithubSourceApiUrl.IsNullOrWhiteSpace())
        {
            GithubSourceApiUrl = System.Environment.GetEnvironmentVariable("GH_SOURCE_API_URL");
        }

        if (GhesApiUrl.HasValue() && GithubSourceApiUrl.HasValue())
        {
            throw new OctoshiftCliException("Only one of --github-source-api-url or --ghes-api-url may be specified.");
        }

        if (NoSslVerify && GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
        }
    }
}
