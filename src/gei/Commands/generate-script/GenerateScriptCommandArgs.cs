using System.IO;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class GenerateScriptCommandArgs : CommandArgs
{
    [LogName("GITHUB SOURCE ORG")]
    public string GithubSourceOrg { get; set; }

    [LogName("ADO SERVER URL")]
    public string AdoServerUrl { get; set; }

    [LogName("ADO SOURCE ORG")]
    public string AdoSourceOrg { get; set; }

    [LogName("ADO TEAM PROJECT")]
    public string AdoTeamProject { get; set; }

    [LogName("GITHUB TARGET ORG")]
    public string GithubTargetOrg { get; set; }

    [LogName("OUTPUT")]
    public FileInfo Output { get; set; }

    [LogName("GHES API URL")]
    public string GhesApiUrl { get; set; }

    [LogName("AWS BUCKET NAME")]
    public string AwsBucketName { get; set; }

    [LogName("NO SSL VERIFY")]
    public bool NoSslVerify { get; set; }

    [LogName("SKIP RELEASES")]
    public bool SkipReleases { get; set; }

    [LogName("LOCK SOURCE REPO")]
    public bool LockSourceRepo { get; set; }

    [LogName("DOWNLOAD MIGRATION LOGS")]
    public bool DownloadMigrationLogs { get; set; }

    [LogName("SEQUENTIAL")]
    public bool Sequential { get; set; }

    [LogName("GITHUB SOURCE PAT")]
    [Secret]
    public string GithubSourcePat { get; set; }

    [LogName("ADO PAT")]
    [Secret]
    public string AdoPat { get; set; }



    public override void Validate()
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
