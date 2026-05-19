using System.Linq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.MigrateRepo;

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

    public string GitlabServerUrl { get; set; }
    public string GitlabGroup { get; set; }
    public string GitlabProject { get; set; }
    [Secret]
    public string GitlabPat { get; set; }
    public bool NoSslVerify { get; set; }

    public bool KeepArchive { get; set; }
    public bool UseGithubStorage { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (!GitlabServerUrl.HasValue() && !ArchiveUrl.HasValue() && !ArchivePath.HasValue())
        {
            throw new OctoshiftCliException("Either --gitlab-server-url, --archive-path, or --archive-url must be specified.");
        }

        if (ArchivePath.HasValue() && ArchiveUrl.HasValue())
        {
            throw new OctoshiftCliException("Only one of --archive-path or --archive-url can be specified.");
        }

        if (ShouldGenerateArchive())
        {
            ValidateGenerateOptions();
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
    }

    private void ValidateNoGenerateOptions()
    {
        if (GitlabPat.HasValue())
        {
            throw new OctoshiftCliException("--gitlab-pat cannot be provided with --archive-path or --archive-url.");
        }

        if (NoSslVerify)
        {
            throw new OctoshiftCliException("--no-ssl-verify cannot be provided with --archive-path or --archive-url.");
        }
    }

    public bool ShouldGenerateArchive() => GitlabServerUrl.HasValue() && !ArchiveUrl.HasValue();

    public bool ShouldUploadArchive() => ArchiveUrl.IsNullOrWhiteSpace() && GithubOrg.HasValue();

    // NOTE: ArchiveUrl doesn't necessarily refer to the value passed in by the user to the CLI - it is set during CLI runtime when an archive is uploaded to blob storage
    public bool ShouldImportArchive() => ArchiveUrl.HasValue() || GithubOrg.HasValue();

    private void ValidateGenerateOptions()
    {
        if (GitlabGroup.IsNullOrWhiteSpace() || GitlabProject.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("Both --gitlab-group and --gitlab-project must be provided.");
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
            throw new OctoshiftCliException("--github-org must be provided in order to import the GitLab archive.");
        }

        if (GithubRepo.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--github-repo must be provided in order to import the GitLab archive.");
        }
    }
}
