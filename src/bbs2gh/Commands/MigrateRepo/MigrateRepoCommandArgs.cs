namespace OctoshiftCLI.BbsToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandArgs
{
    public string ArchiveUrl { get; set; }
    public string ArchivePath { get; set; }

    public string AzureStorageConnectionString { get; set; }

    public string AwsBucketName { get; set; }
    public string AwsAccessKey { get; set; }
    public string AwsSecretKey { get; set; }
    public string AwsSessionToken { get; set; }
    public string AwsRegion { get; set; }

    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string GithubPat { get; set; }
    public bool Wait { get; set; }
    public bool QueueOnly { get; set; }
    public string TargetRepoVisibility { get; set; }
    public bool Kerberos { get; set; }
    public bool Verbose { get; set; }

    public string BbsServerUrl { get; set; }
    public string BbsProject { get; set; }
    public string BbsRepo { get; set; }
    public string BbsUsername { get; set; }
    public string BbsPassword { get; set; }
    public string BbsSharedHome { get; set; }
    public bool NoSslVerify { get; set; }


    public string ArchiveDownloadHost { get; set; }
    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public int SshPort { get; set; } = 22;

    public string SmbUser { get; set; }
    public string SmbPassword { get; set; }
    public string SmbDomain { get; set; }

    public bool KeepArchive { get; set; }
}
