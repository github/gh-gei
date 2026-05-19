using System.IO;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.GenerateScript;

public class GenerateScriptCommandArgs : CommandArgs
{
    public string GitlabServerUrl { get; set; }
    public string GithubOrg { get; set; }
    [Secret]
    public string GitlabPat { get; set; }
    public string GitlabGroup { get; set; }
    public string GitlabProject { get; set; }
    public bool NoSslVerify { get; set; }
    public FileInfo Output { get; set; }
    public string AwsBucketName { get; set; }
    public string AwsRegion { get; set; }
    public bool KeepArchive { get; set; }
    public string TargetApiUrl { get; set; }
    public string TargetUploadsUrl { get; set; }
    public bool UseGithubStorage { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GitlabProject.HasValue() && GitlabGroup.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-group must be provided when --gitlab-project is specified.");
        }

        if (AwsBucketName.HasValue() && UseGithubStorage)
        {
            throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.");
        }

        if (AwsRegion.HasValue() && UseGithubStorage)
        {
            throw new OctoshiftCliException("The --use-github-storage flag was provided with an AWS S3 region. Archive cannot be uploaded to both locations.");
        }
    }
}
