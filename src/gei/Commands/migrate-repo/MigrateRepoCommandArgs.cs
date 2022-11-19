namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class MigrateRepoCommandArgs : CommandArgs
{
    public string GithubSourceOrg { get; set; }
    public string AdoServerUrl { get; set; }
    public string AdoSourceOrg { get; set; }
    public string AdoTeamProject { get; set; }
    public string SourceRepo { get; set; }
    public string GithubTargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GhesApiUrl { get; set; }
    [Secret]
    public string AzureStorageConnectionString { get; set; }
    public string AwsBucketName { get; set; }
    [Secret]
    public string AwsAccessKey { get; set; }
    [Secret]
    public string AwsSecretKey { get; set; }
    public bool NoSslVerify { get; set; }
    public string GitArchiveUrl { get; set; }
    public string MetadataArchiveUrl { get; set; }
    public bool SkipReleases { get; set; }
    public bool LockSourceRepo { get; set; }
    public bool Wait { get; set; }
    [Secret]
    public string GithubSourcePat { get; set; }
    [Secret]
    public string GithubTargetPat { get; set; }
    [Secret]
    public string AdoPat { get; set; }
}
