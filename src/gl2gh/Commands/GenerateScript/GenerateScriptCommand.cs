using System;
using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.GenerateScript;

public class GenerateScriptCommand : CommandBase<GenerateScriptCommandArgs, GenerateScriptCommandHandler>
{
    public GenerateScriptCommand() : base(
            name: "generate-script",
            description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.")
    {
        AddOption(GitlabServerUrl);
        AddOption(GithubOrg);
        AddOption(TargetApiUrl);
        AddOption(TargetUploadsUrl);
        AddOption(GitlabPat);
        AddOption(GitlabGroup);
        AddOption(GitlabProject);
        AddOption(Output);
        AddOption(Verbose);
        AddOption(AwsBucketName);
        AddOption(AwsRegion);
        AddOption(KeepArchive);
        AddOption(NoSslVerify);
        AddOption(UseGithubStorage);
    }

    public Option<string> GitlabServerUrl { get; } = new(
        name: "--gitlab-server-url",
        description: "The full URL of the GitLab server to migrate from, e.g. https://gitlab.mycompany.com")
    { IsRequired = true };

    public Option<string> GitlabPat { get; } = new(
        name: "--gitlab-pat",
        description: "The GitLab PAT of a user with admin privileges to get the list of all groups and their projects. If not set will be read from GITLAB_PAT environment variable." +
                      $"{Environment.NewLine}" +
                      "Note: The PAT will not get included in the generated script and it has to be set as an env variable before running the script.");

    public Option<string> GitlabGroup { get; } = new(
        name: "--gitlab-group",
        description: "The GitLab group to migrate. If not set will migrate all groups the user has access to.");

    public Option<string> GitlabProject { get; } = new(
        name: "--gitlab-project",
        description: "The GitLab project to migrate. Requires --gitlab-group. If not set will migrate all projects in the group.");

    public Option<string> GithubOrg { get; } = new("--github-org")
    { IsRequired = true };

    public Option<FileInfo> Output { get; } = new(
        name: "--output",
        getDefaultValue: () => new FileInfo("./migrate.ps1"));

    public Option<string> AwsBucketName { get; } = new(
        name: "--aws-bucket-name",
        description: "If using AWS, the name of the S3 bucket to upload the GitLab archive to.");

    public Option<string> AwsRegion { get; } = new(
        name: "--aws-region",
        description: "If using AWS, the AWS region. If not provided, it will be read from AWS_REGION environment variable. " +
                     "Required if using AWS.");

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
        description: "The URL of the target uploads API, if not migrating to github.com. Defaults to https://uploads.github.com")
    { IsHidden = true };

    public Option<bool> NoSslVerify { get; } = new(
        name: "--no-ssl-verify",
        description: "Disables SSL verification when communicating with your GitLab instance. All other migration steps will continue to verify SSL. " +
                     "If your GitLab instance has a self-signed SSL certificate, this flag will allow the migration archive to be exported.");

    public Option<bool> UseGithubStorage { get; } = new("--use-github-storage")
    {
        IsHidden = true,
        Description = "Enables multipart uploads to a GitHub owned storage for use during migration. " +
                      "Configure chunk size with the GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES environment variable (default: 100 MiB, minimum: 5 MiB).",
    };

    public override GenerateScriptCommandHandler BuildHandler(GenerateScriptCommandArgs args, IServiceProvider sp)
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
        var versionProvider = sp.GetRequiredService<IVersionProvider>();
        var fileSystemProvider = sp.GetRequiredService<FileSystemProvider>();

        var gitlabApiFactory = sp.GetRequiredService<GitlabApiFactory>();
        var gitlabApi = gitlabApiFactory.Create(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify);

        return new GenerateScriptCommandHandler(log, versionProvider, fileSystemProvider, gitlabApi);
    }
}
