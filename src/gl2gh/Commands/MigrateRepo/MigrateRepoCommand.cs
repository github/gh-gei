using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Factories;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.MigrateRepo;

public class MigrateRepoCommand : CommandBase<MigrateRepoCommandArgs, MigrateRepoCommandHandler>
{
    public MigrateRepoCommand() : base(
            name: "migrate-repo",
            description: "Import a GitLab archive to GitHub." +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
    {
        AddOption(ArchiveUrl);
        AddOption(GithubOrg);
        AddOption(GithubRepo);
        AddOption(GithubPat);
        AddOption(GitlabServerUrl);
        AddOption(GitlabGroup);
        AddOption(GitlabProject);
        AddOption(GitlabPat);
        AddOption(ArchivePath);
        AddOption(AzureStorageConnectionString);
        AddOption(AwsBucketName);
        AddOption(AwsAccessKey);
        AddOption(AwsSecretKey);
        AddOption(AwsSessionToken);
        AddOption(AwsRegion);
        AddOption(QueueOnly);
        AddOption(TargetRepoVisibility.FromAmong("public", "private", "internal"));
        AddOption(Verbose);
        AddOption(KeepArchive);
        AddOption(NoSslVerify);
        AddOption(TargetApiUrl);
        AddOption(TargetUploadsUrl);
        AddOption(UseGithubStorage);
    }

    public Option<string> GitlabServerUrl { get; } = new(
        name: "--gitlab-server-url",
        description: "The full URL of the GitLab server, e.g. https://gitlab.mycompany.com");

    public Option<string> GitlabGroup { get; } = new(
        name: "--gitlab-group",
        description: "The GitLab group (full namespace path) that contains the project to migrate. For nested subgroups, use the full path, e.g. parent-group/subgroup.");

    public Option<string> GitlabProject { get; } = new(
        name: "--gitlab-project",
        description: "The GitLab project to migrate.");

    public Option<string> GitlabPat { get; } = new(
        name: "--gitlab-pat",
        description: "The GitLab PAT. If not passed, it will read the PAT from the GITLAB_PAT environment variable.");

    public Option<string> ArchiveUrl { get; } = new(
        name: "--archive-url",
        description:
        "URL used to download the GitLab migration archive. Only needed if you want to manually retrieve the archive from GitLab instead of letting this CLI do that for you.");

    public Option<string> ArchivePath { get; } = new(
        name: "--archive-path",
        description: "Path to the GitLab migration archive on disk. When --gitlab-server-url is provided, the generated archive will be written to this path (overwriting any existing file).");

    public Option<string> AzureStorageConnectionString { get; } = new(
        name: "--azure-storage-connection-string",
        description: "A connection string for an Azure Storage account, used to upload the GitLab archive. If not passed, it will read the AZURE_STORAGE_CONNECTION_STRING environment variable.");

    public Option<string> AwsBucketName { get; } = new(
        name: "--aws-bucket-name",
        description: "If using AWS, the name of the S3 bucket to upload the GitLab archive to.");

    public Option<string> AwsAccessKey { get; } = new(
        name: "--aws-access-key",
        description: "If uploading to S3, the AWS access key. If not provided, it will be read from AWS_ACCESS_KEY_ID environment variable.");

    public Option<string> AwsSecretKey { get; } = new(
        name: "--aws-secret-key",
        description: "If uploading to S3, the AWS secret key. If not provided, it will be read from AWS_SECRET_ACCESS_KEY environment variable.");

    public Option<string> AwsSessionToken { get; } = new(
        name: "--aws-session-token",
        description: "If using AWS, the AWS session token. If not provided, it will be read from AWS_SESSION_TOKEN environment variable.");

    public Option<string> AwsRegion { get; } = new(
        name: "--aws-region",
        description: "If using AWS, the AWS region. If not provided, it will be read from AWS_REGION environment variable. " +
                     "Required if using AWS.");

    public Option<string> GithubOrg { get; } = new("--github-org");

    public Option<string> GithubRepo { get; } = new("--github-repo");

    public Option<string> GithubPat { get; } = new(
        name: "--github-pat",
        description: "The GitHub personal access token to be used for the migration. If not set will be read from GH_PAT environment variable.");

    public Option<bool> QueueOnly { get; } = new(
        name: "--queue-only",
        description: "Only queues the migration, does not wait for it to finish. Use the wait-for-migration command to subsequently wait for it to finish and view the status.");

    public Option<string> TargetRepoVisibility { get; } = new(
        name: "--target-repo-visibility",
        description: "The visibility of the target repo. Defaults to private. Valid values are public, private, or internal.");

    public Option<bool> Verbose { get; } = new("--verbose");

    public Option<bool> KeepArchive { get; } = new(
        name: "--keep-archive",
        description: "Keeps the downloaded export archive after successfully uploading it. By default, it will be automatically deleted.");
    public Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
    };
    public Option<string> TargetUploadsUrl { get; } = new(
        name: "--target-uploads-url",
        description: "The URL of the target uploads API, if not migrating to github.com. Defaults to https://uploads.github.com");
    public Option<bool> NoSslVerify { get; } = new(
        name: "--no-ssl-verify",
        description: "Disables SSL verification when communicating with your GitLab instance. All other migration steps will continue to verify SSL. " +
                     "If your GitLab instance has a self-signed SSL certificate, this flag will allow the migration archive to be exported.");
    public Option<bool> UseGithubStorage { get; } = new(
        name: "--use-github-storage",
        description: "Enables multipart uploads to a GitHub owned storage for use during migration. " +
                     "Configure chunk size with the GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES environment variable (default: 100 MiB, minimum: 5 MiB).");

    public override MigrateRepoCommandHandler BuildHandler(MigrateRepoCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
        var fileSystemProvider = sp.GetRequiredService<FileSystemProvider>();

        GithubApi githubApi = null;
        GitlabApi gitlabApi = null;
        AzureApi azureApi = null;
        AwsApi awsApi = null;

        if (args.GithubOrg.HasValue())
        {
            var githubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
            githubApi = githubApiFactory.Create(args.TargetApiUrl, args.TargetUploadsUrl, args.GithubPat);
        }

        if (args.GitlabServerUrl.HasValue())
        {
            var gitlabApiFactory = sp.GetRequiredService<GitlabApiFactory>();

            gitlabApi = gitlabApiFactory.Create(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify);
        }

        var azureStorageConnectionString = args.AzureStorageConnectionString ?? environmentVariableProvider.AzureStorageConnectionString(false);
        if (azureStorageConnectionString.HasValue())
        {
            var azureApiFactory = sp.GetRequiredService<IAzureApiFactory>();
            azureApi = azureApiFactory.Create(azureStorageConnectionString);
        }

        if (args.AwsBucketName.HasValue())
        {
            var awsApiFactory = sp.GetRequiredService<AwsApiFactory>();
            awsApi = awsApiFactory.Create(args.AwsRegion, args.AwsAccessKey, args.AwsSecretKey, args.AwsSessionToken);
        }

        var warningsCountLogger = sp.GetRequiredService<WarningsCountLogger>();

        return new MigrateRepoCommandHandler(log, githubApi, gitlabApi, environmentVariableProvider, azureApi, awsApi, fileSystemProvider, warningsCountLogger);
    }
}
