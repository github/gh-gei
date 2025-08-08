using System.Linq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

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
    public bool QueueOnly { get; set; }
    public string TargetRepoVisibility { get; set; }
    public string TargetApiUrl { get; set; }
    public string TargetUploadsUrl { get; set; }
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
    public bool UseGithubStorage { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (!BbsServerUrl.HasValue() && !ArchiveUrl.HasValue() && !ArchivePath.HasValue())
        {
            throw new OctoshiftCliException("Either --bbs-server-url, --archive-path, or --archive-url must be specified.");
        }

        if (ArchivePath.HasValue() && ArchiveUrl.HasValue())
        {
            throw new OctoshiftCliException("Only one of --archive-path or --archive-url can be specified.");
        }

        if (ShouldGenerateArchive())
        {
            ValidateGenerateOptions();
            ValidateDownloadOptions();
        }
        else
        {
            ValidateNoGenerateOptions();
        }

        if (ShouldUploadArchive())
        {
            ValidateUploadOptions();
        }

        if (ShouldImportArchive())
        {
            ValidateImportOptions();
        }

        if (SshPort == 7999)
        {
            log?.LogWarning("--ssh-port is set to 7999, which is the default port that Bitbucket Server and Bitbucket Data Center use for Git operations over SSH. This is probably the wrong value, because --ssh-port should be configured with the SSH port used to manage the server where Bitbucket Server/Bitbucket Data Center is running, not the port used for Git operations over SSH.");
        }
    }

    private void ValidateNoGenerateOptions()
    {
        if (BbsUsername.HasValue() || BbsPassword.HasValue())
        {
            throw new OctoshiftCliException("--bbs-username and --bbs-password cannot be provided with --archive-path or --archive-url.");
        }

        if (NoSslVerify)
        {
            throw new OctoshiftCliException("--no-ssl-verify cannot be provided with --archive-path or --archive-url.");
        }

        if (new[] { SshUser, SshPrivateKey, ArchiveDownloadHost, SmbUser, SmbPassword, SmbDomain }.Any(obj => obj.HasValue()))
        {
            throw new OctoshiftCliException("SSH or SMB download options cannot be provided with --archive-path or --archive-url.");
        }
    }

    public bool ShouldGenerateArchive() => BbsServerUrl.HasValue() && !ArchivePath.HasValue() && !ArchiveUrl.HasValue();

    public bool ShouldDownloadArchive() => SshUser.HasValue() || SmbUser.HasValue();

    public bool ShouldUploadArchive() => ArchiveUrl.IsNullOrWhiteSpace() && GithubOrg.HasValue();

    // NOTE: ArchiveUrl doesn't necessarily refer to the value passed in by the user to the CLI - it is set during CLI runtime when an archive is uploaded to blob storage
    public bool ShouldImportArchive() => ArchiveUrl.HasValue() || GithubOrg.HasValue();

    private void ValidateGenerateOptions()
    {
        if (Kerberos && (BbsUsername.HasValue() || BbsPassword.HasValue()))
        {
            throw new OctoshiftCliException("--bbs-username and --bbs-password cannot be provided with --kerberos.");
        }

        if (BbsProject.IsNullOrWhiteSpace() || BbsRepo.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("Both --bbs-project and --bbs-repo must be provided.");
        }
    }

    private void ValidateDownloadOptions()
    {
        var sshArgs = new[] { SshUser, SshPrivateKey };
        var smbArgs = new[] { SmbUser, SmbPassword };
        var shouldUseSsh = sshArgs.Any(arg => arg.HasValue());
        var shouldUseSmb = smbArgs.Any(arg => arg.HasValue());

        if (shouldUseSsh && shouldUseSmb)
        {
            throw new OctoshiftCliException("You can't provide both SSH and SMB credentials together.");
        }

        if (SshUser.HasValue() ^ SshPrivateKey.HasValue())
        {
            throw new OctoshiftCliException("Both --ssh-user and --ssh-private-key must be specified for SSH download.");
        }

        if (ArchiveDownloadHost.HasValue() && !shouldUseSsh && !shouldUseSmb)
        {
            throw new OctoshiftCliException("--archive-download-host can only be provided if SSH or SMB download options are provided.");
        }
    }

    private void ValidateUploadOptions()
    {
        if (AwsBucketName.IsNullOrWhiteSpace() && new[] { AwsAccessKey, AwsSecretKey, AwsSessionToken, AwsRegion }.Any(x => x.HasValue()))
        {
            throw new OctoshiftCliException("The AWS S3 bucket name must be provided with --aws-bucket-name if other AWS S3 upload options are set.");
        }
        if (UseGithubStorage && AwsBucketName.HasValue())
        {
            throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.");
        }
        if (AzureStorageConnectionString.HasValue() && UseGithubStorage)
        {
            throw new OctoshiftCliException("The --use-github-storage flag was provided with a connection string for an Azure storage account. Archive cannot be uploaded to both locations.");
        }
    }

    private void ValidateImportOptions()
    {
        if (GithubOrg.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--github-org must be provided in order to import the Bitbucket archive.");
        }

        if (GithubRepo.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--github-repo must be provided in order to import the Bitbucket archive.");
        }
    }
}
