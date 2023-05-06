using OctoshiftCLI.Commands;

namespace OctoshiftCLI.BbsToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandArgs : CommandArgs
{
    public string ArchiveUrl { get; set; }
    public string ArchivePath { get; set; }

    [Secret]
    public string AzureStorageConnectionString { get; set; }

    public string AwsBucketName { get; set; }
    [Secret]
    public string AwsAccessKey { get; set; }
    [Secret]
    public string AwsSecretKey { get; set; }
    [Secret]
    public string AwsSessionToken { get; set; }
    public string AwsRegion { get; set; }

    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public bool Wait { get; set; }
    public bool QueueOnly { get; set; }
    public string TargetRepoVisibility { get; set; }
    public bool Kerberos { get; set; }

    public string BbsServerUrl { get; set; }
    public string BbsProject { get; set; }
    public string BbsRepo { get; set; }
    public string BbsUsername { get; set; }
    [Secret]
    public string BbsPassword { get; set; }
    public string BbsSharedHome { get; set; }
    public bool NoSslVerify { get; set; }


    public string ArchiveDownloadHost { get; set; }
    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public int SshPort { get; set; } = 22;

    public string SmbUser { get; set; }
    [Secret]
    public string SmbPassword { get; set; }
    public string SmbDomain { get; set; }

    public bool KeepArchive { get; set; }
}
