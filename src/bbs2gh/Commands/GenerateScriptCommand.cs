using System;
using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.BbsToGithub.Handlers;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class GenerateScriptCommand : CommandBase<GenerateScriptCommandArgs, GenerateScriptCommandHandler>
{
    public GenerateScriptCommand() : base(
            name: "generate-script",
            description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.")
    {
        AddOption(BbsServerUrl);
        AddOption(GithubOrg);
        AddOption(BbsUsername);
        AddOption(BbsPassword);
        AddOption(BbsProjectKey);
        AddOption(BbsSharedHome);
        AddOption(SshUser);
        AddOption(SshPrivateKey);
        AddOption(SshPort);
        AddOption(SmbUser);
        AddOption(SmbDomain);
        AddOption(Output);
        AddOption(Kerberos);
        AddOption(Verbose);
        AddOption(AwsBucketName);
        AddOption(AwsRegion);
        AddOption(KeepArchive);
        AddOption(NoSslVerify);
    }

    public Option<string> BbsServerUrl { get; } = new(
        name: "--bbs-server-url",
        description: "The full URL of the Bitbucket Server/Data Center to migrate from.")
    { IsRequired = true };

    public Option<string> BbsUsername { get; } = new(
        name: "--bbs-username",
        description: "The Bitbucket username of a user with site admin privileges to get the list of all projects and their repos. If not set will be read from BBS_USERNAME environment variable.");

    public Option<string> BbsPassword { get; } = new(
        name: "--bbs-password",
        description: "The Bitbucket password of a user with site admin privileges to get the list of all projects and their repos. If not set will be read from BBS_PASSWORD environment variable." +
                      $"{Environment.NewLine}" +
                      "Note: The password will not get included in the generated script and it has to be set as an env variable before running the script.");

    public Option<string> BbsProjectKey { get; } = new(
        name: "--bbs-project-key",
        description: "The Bitbucket project to migrate. If not set will migrate all projects.");

    public Option<string> BbsSharedHome { get; } = new(
        name: "--bbs-shared-home",
        description: "Bitbucket server's shared home directory. Defaults to \"/var/atlassian/application-data/bitbucket/shared\" if downloading the archive from a server using SSH " +
                     "and \"c$\\atlassian\\applicationdata\\bitbucket\\shared\" if downloading using SMB.");

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
                     "Defaults to us-east-1 if neither the argument nor the environment variable is set. " +
                     "In a future release, you will be required to set an AWS region if using AWS S3 as your blob storage provider.");

    public Option<bool> Verbose { get; } = new("--verbose");

    public Option<bool> KeepArchive { get; } = new(
        name: "--keep-archive",
        description: "Keeps the downloaded export archive after successfully uploading it. By default, it will be automatically deleted.");

    public Option<bool> NoSslVerify { get; } = new(
        name: "--no-ssl-verify",
        description: "Disables SSL verification when communicating with your Bitbucket Server/Data Center instance. All other migration steps will continue to verify SSL. " +
                     "If your Bitbucket instance has a self-signed SSL certificate then setting this flag will allow the migration archive to be exported.");

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

        var bbsApiFactory = sp.GetRequiredService<BbsApiFactory>();
        var bbsApi = args.Kerberos
            ? bbsApiFactory.CreateKerberos(args.BbsServerUrl, args.NoSslVerify)
            : bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername, args.BbsPassword, args.NoSslVerify);

        return new GenerateScriptCommandHandler(log, versionProvider, fileSystemProvider, bbsApi, environmentVariableProvider);
    }
}

public class GenerateScriptCommandArgs
{
    public string BbsServerUrl { get; set; }
    public string GithubOrg { get; set; }
    public string BbsUsername { get; set; }
    public string BbsPassword { get; set; }
    public string BbsProjectKey { get; set; }
    public string BbsSharedHome { get; set; }
    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public int SshPort { get; set; }
    public string SmbUser { get; set; }
    public string SmbDomain { get; set; }
    public FileInfo Output { get; set; }
    public bool Kerberos { get; set; }
    public bool Verbose { get; set; }
    public string AwsBucketName { get; set; }
    public string AwsRegion { get; set; }
    public bool KeepArchive { get; set; }
    public bool NoSslVerify { get; set; }
}
