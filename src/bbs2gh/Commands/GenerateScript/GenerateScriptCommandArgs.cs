using System.IO;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Commands.GenerateScript;

public class GenerateScriptCommandArgs : CommandArgs
{
    public string BbsServerUrl { get; set; }
    public string GithubOrg { get; set; }
    public string BbsUsername { get; set; }
    [Secret]
    public string BbsPassword { get; set; }
    public string BbsProject { get; set; }
    public string BbsSharedHome { get; set; }
    public string ArchiveDownloadHost { get; set; }
    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public int SshPort { get; set; }
    public string SmbUser { get; set; }
    public string SmbDomain { get; set; }
    public FileInfo Output { get; set; }
    public bool Kerberos { get; set; }
    public string AwsBucketName { get; set; }
    public string AwsRegion { get; set; }
    public bool KeepArchive { get; set; }
    public bool NoSslVerify { get; set; }
    public string TargetApiUrl { get; set; }
    public string TargetUploadsUrl { get; set; }
    public bool UseGithubStorage { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (NoSslVerify && BbsServerUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--no-ssl-verify can only be provided with --bbs-server-url.");
        }

        if (AwsBucketName.HasValue() && UseGithubStorage)
        {
            throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.");
        }

        if (AwsRegion.HasValue() && UseGithubStorage)
        {
            throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 region. Archive cannot be uploaded to both locations.");
        }

        if (SshPort == 7999)
        {
            log?.LogWarning("--ssh-port is set to 7999, which is the default port that Bitbucket Server and Bitbucket Data Center use for Git operations over SSH. This is probably the wrong value, because --ssh-port should be configured with the SSH port used to manage the server where Bitbucket Server/Bitbucket Data Center is running, not the port used for Git operations over SSH.");
        }
    }
}
