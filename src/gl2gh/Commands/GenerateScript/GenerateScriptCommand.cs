using System;
using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
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
        AddOption(GitlabPat);
        AddOption(GitlabProject);
        AddOption(GitlabSharedHome);
        AddOption(SshUser);
        AddOption(SshPrivateKey);
        AddOption(SshPort);
        AddOption(ArchiveDownloadHost);
        AddOption(SmbUser);
        AddOption(SmbDomain);
        AddOption(Output);
        AddOption(Kerberos);
        AddOption(Verbose);
        AddOption(AwsBucketName);
        AddOption(AwsRegion);
        AddOption(KeepArchive);
        AddOption(NoSslVerify);
        AddOption(UseGithubStorage);
    }

    public Option<string> GitlabServerUrl { get; } = new(
        name: "--bbs-server-url",
        description: "The full URL of the Bitbucket Server/Data Center to migrate from.")
    { IsRequired = true };

    public Option<string> GitlabPat { get; } = new(
        name: "--bbs-pat",
        description: "The Bitbucket PAT of a user with site admin privileges to get the list of all projects and their repos. If not set will be read from BBS_PASSWORD environment variable." +
                      $"{Environment.NewLine}" +
                      "Note: The PAT will not get included in the generated script and it has to be set as an env variable before running the script.");

    public Option<string> GitlabProject { get; } = new(
        name: "--bbs-project",
        description: "The Bitbucket project to migrate. If not set will migrate all projects.");

    public Option<string> GitlabSharedHome { get; } = new(
        name: "--bbs-shared-home",
        description: "Bitbucket server's shared home directory. Defaults to \"/var/atlassian/application-data/bitbucket/shared\" if downloading the archive from a server using SSH " +
                     "and \"c$\\atlassian\\applicationdata\\bitbucket\\shared\" if downloading using SMB.");

    public Option<string> ArchiveDownloadHost { get; } = new(
        name: "--archive-download-host",
        description: "The host to use to connect to the Bitbucket Server/Data Center instance via SSH or SMB. Defaults to the host from the Bitbucket Server URL (--bbs-server-url).");

    public Option<string> SshUser { get; } = new(
        name: "--ssh-user",
        description: "The SSH user to be used for downloading the export archive off of the Bitbucket server.");

    public Option<string> SshPrivateKey { get; } = new(
        name: "--ssh-private-key",
        description: "The full path of the private key file to be used for downloading the export archive off of the Bitbucket Server using SSH/SFTP.");

    public Option<int> SshPort { get; } = new(
        name: "--ssh-port",
        description: "The SSH port (default: 22).",
        getDefaultValue: () => 22);

    public Option<string> SmbUser { get; } = new(
        name: "--smb-user",
        description: "The SMB user used for authentication when downloading the export archive from the Bitbucket Server instance." +
                     $"{Environment.NewLine}" +
                     "Note: You must also specify the SMB password using the SMB_PASSWORD environment variable.");

    public Option<string> SmbDomain { get; } = new(
        name: "--smb-domain",
        description: "The optional domain name when using SMB for downloading the export archive.");

    public Option<string> GithubOrg { get; } = new("--github-org")
    { IsRequired = true };

    public Option<FileInfo> Output { get; } = new(
        name: "--output",
        getDefaultValue: () => new FileInfo("./migrate.ps1"));

    public Option<bool> Kerberos { get; } = new(
        name: "--kerberos",
        description: "Use Kerberos authentication for Bitbucket Server.")
    { IsHidden = true };

    public Option<string> AwsBucketName { get; } = new(
        name: "--aws-bucket-name",
        description: "If using AWS, the name of the S3 bucket to upload the BBS archive to.");

    public Option<string> AwsRegion { get; } = new(
        name: "--aws-region",
        description: "If using AWS, the AWS region. If not provided, it will be read from AWS_REGION environment variable. " +
                     "Required if using AWS.");

    public Option<bool> Verbose { get; } = new("--verbose");

    public Option<bool> KeepArchive { get; } = new(
        name: "--keep-archive",
        description: "Keeps the downloaded export archive after successfully uploading it. By default, it will be automatically deleted.");

    public Option<bool> NoSslVerify { get; } = new(
        name: "--no-ssl-verify",
        description: "Disables SSL verification when communicating with your Bitbucket Server/Data Center instance. All other migration steps will continue to verify SSL. " +
                     "If your Bitbucket instance has a self-signed SSL certificate then setting this flag will allow the migration archive to be exported.");
    public Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
    };

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
        var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();

        var gitlabApiFactory = sp.GetRequiredService<GitlabApiFactory>();
        var gitlabApi = args.Kerberos
            ? gitlabApiFactory.CreateKerberos(args.GitlabServerUrl, args.NoSslVerify)
            : gitlabApiFactory.Create(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify);

        return new GenerateScriptCommandHandler(log, versionProvider, fileSystemProvider, gitlabApi, environmentVariableProvider);
    }
}
