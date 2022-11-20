using System.IO;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class GenerateScriptCommandArgs : CommandArgs
{
    public string GithubSourceOrg { get; set; }
    public string AdoServerUrl { get; set; }
    public string AdoSourceOrg { get; set; }
    public string AdoTeamProject { get; set; }
    public string GithubTargetOrg { get; set; }
    public FileInfo Output { get; set; }
    public string GhesApiUrl { get; set; }
    public string AwsBucketName { get; set; }
    public bool NoSslVerify { get; set; }
    public bool SkipReleases { get; set; }
    public bool LockSourceRepo { get; set; }
    public bool DownloadMigrationLogs { get; set; }
    public bool Sequential { get; set; }
    [Secret]
    public string GithubSourcePat { get; set; }
    [Secret]
    public string AdoPat { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GithubSourceOrg.IsNullOrWhiteSpace() && AdoSourceOrg.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
        }

        if (AdoServerUrl.HasValue() && AdoSourceOrg.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("Must specify --ado-source-org with the collection name when using --ado-server-url");
        }

        if (AwsBucketName.HasValue() && GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url must be specified when --aws-bucket-name is specified.");
        }

        if (NoSslVerify && GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
        }
    }
}
