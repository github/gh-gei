using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.BbsToGithub.Handlers;
using OctoshiftCLI.BbsToGithub.Services;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class MigrateRepoCommand : CommandBase<MigrateRepoCommandArgs, MigrateRepoCommandHandler>
{
    public MigrateRepoCommand() : base(
            name: "migrate-repo",
            description: "Import a Bitbucket Server archive to GitHub." +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
    {
        AddOption(ArchiveUrl);
        AddOption(GithubOrg);
        AddOption(GithubRepo);
        AddOption(GithubPat);
        AddOption(BbsServerUrl);
        AddOption(BbsProject);
        AddOption(BbsRepo);
        AddOption(BbsUsername);
        AddOption(BbsPassword);
        AddOption(BbsSharedHome);
        AddOption(SshUser);
        AddOption(SshPrivateKey);
        AddOption(SshPort);
        AddOption(ArchiveDownloadHost);
        AddOption(SmbUser);
        AddOption(SmbPassword);
        AddOption(SmbDomain);
        AddOption(ArchivePath);
        AddOption(AzureStorageConnectionString);
        AddOption(AwsBucketName);
        AddOption(AwsAccessKey);
        AddOption(AwsSecretKey);
        AddOption(AwsSessionToken);
        AddOption(AwsRegion);
        AddOption(Wait);
        AddOption(Kerberos);
        AddOption(Verbose);
        AddOption(KeepArchive);
        AddOption(NoSslVerify);
    }

    public Option<string> BbsServerUrl { get; } = new(
        name: "--bbs-server-url",
        description: "The full URL of the Bitbucket Server/Data Center to migrate from. E.g. http://bitbucket.contoso.com:7990");

    public Option<string> BbsProject { get; } = new(
        name: "--bbs-project",
        description: "The Bitbucket project to migrate.");

    public Option<string> BbsRepo { get; } = new(
        name: "--bbs-repo",
        description: "The Bitbucket repository to migrate.");

    public Option<string> BbsUsername { get; } = new(
        name: "--bbs-username",
        description: "The Bitbucket username of a user with site admin privileges. If not set will be read from BBS_USERNAME environment variable.");

    public Option<string> BbsPassword { get; } = new(
        name: "--bbs-password",
        description: "The Bitbucket password of the user specified by --bbs-username. If not set will be read from BBS_PASSWORD environment variable.");

    public Option<string> BbsSharedHome { get; } = new(
        name: "--bbs-shared-home",
        description: "Bitbucket server's shared home directory. Defaults to \"/var/atlassian/application-data/bitbucket/shared\" if downloading the archive from a server using SSH " +
                     "and \"c$\\atlassian\\applicationdata\\bitbucket\\shared\" if downloading using SMB.");

    public Option<string> ArchiveUrl { get; } = new(
        name: "--archive-url",
        description:
        "URL used to download Bitbucket Server migration archive. Only needed if you want to manually retrieve the archive from BBS instead of letting this CLI do that for you.");

    public Option<string> ArchivePath { get; } = new(
        name: "--archive-path",
        description: "Path to Bitbucket Server migration archive on disk.");

    public Option<string> AzureStorageConnectionString { get; } = new(
        name: "--azure-storage-connection-string",
        description: "A connection string for an Azure Storage account, used to upload the BBS archive. If not set will be read from AZURE_STORAGE_CONNECTION_STRING environment variable.");

    public Option<string> AwsBucketName { get; } = new(
        name: "--aws-bucket-name",
        description: "If using AWS, the name of the S3 bucket to upload the BBS archive to.");

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
                     "Defaults to us-east-1 if neither the argument nor the environment variable is set. " +
                     "In a future release, you will be required to set an AWS region if using AWS S3 as your blob storage provider.");

    public Option<string> GithubOrg { get; } = new("--github-org");

    public Option<string> GithubRepo { get; } = new("--github-repo");

    public Option<string> ArchiveDownloadHost { get; } = new(
        name: "--archive-download-host",
        description: "The host to use to connect to the Bitbucket Server/Data Center instance via SSH or SMB. Defaults to the host from the Bitbucket Server URL (--bbs-server-url).")
    { IsHidden = true };

    public Option<string> SshUser { get; } = new(
        name: "--ssh-user",
        description: "The SSH user to be used for downloading the export archive off of the Bitbucket server.");

    public Option<string> SshPrivateKey { get; } = new(
        name: "--ssh-private-key",
        description: "The full path of the private key file to be used for downloading the export archive off of the Bitbucket Server using SSH/SFTP." +
                     Environment.NewLine +
                     "Supported private key formats:" +
                     Environment.NewLine +
                     "  - RSA in OpenSSL PEM format." +
                     Environment.NewLine +
                     "  - DSA in OpenSSL PEM format." +
                     Environment.NewLine +
                     "  - ECDSA 256/384/521 in OpenSSL PEM format." +
                     Environment.NewLine +
                     "  - ECDSA 256/384/521, ED25519 and RSA in OpenSSH key format.");

    public Option<int> SshPort { get; } = new(
        name: "--ssh-port",
        getDefaultValue: () => 22,
        description: "The SSH port (default: 22).");

    public Option<string> SmbUser { get; } = new(
        name: "--smb-user",
        description: "The SMB user used for authentication when downloading the export archive from the Bitbucket Server instance.");

    public Option<string> SmbPassword { get; } = new(
        name: "--smb-password",
        description: "The SMB password used for authentication when downloading the export archive from the Bitbucket server instance. If not provided, it will be read from SMB_PASSWORD environment variable.");

    public Option<string> SmbDomain { get; } = new(
        name: "--smb-domain",
        description: "The optional domain name when using SMB for downloading the export archive.");

    public Option<string> GithubPat { get; } = new(
        name: "--github-pat",
        description: "The GitHub personal access token to be used for the migration. If not set will be read from GH_PAT environment variable.");

    public Option<bool> Wait { get; } = new("--wait")
    {
        IsHidden = true,
        Description = "Synchronously waits for the repo migration to finish.",
    };

    public Option<bool> QueueOnly { get; } = new("--queue-only")
    {
        Description = "Only queues the migration, does not wait for it to finish. Use the wait-for-migration command to subsequently wait for it to finish and view the status."
    };

    public Option<bool> Kerberos { get; } = new(
        name: "--kerberos",
        description: "Use Kerberos authentication for downloading the export archive off of the Bitbucket server.")
    { IsHidden = true };

    public Option<bool> Verbose { get; } = new("--verbose");

    public Option<bool> KeepArchive { get; } = new(
        name: "--keep-archive",
        description: "Keeps the downloaded export archive after successfully uploading it. By default, it will be automatically deleted.");

    public Option<bool> NoSslVerify { get; } = new(
        name: "--no-ssl-verify",
        description: "Disables SSL verification when communicating with your Bitbucket Server/Data Center instance. All other migration steps will continue to verify SSL. " +
                     "If your Bitbucket instance has a self-signed SSL certificate then setting this flag will allow the migration archive to be exported.");

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
        BbsApi bbsApi = null;
        IBbsArchiveDownloader bbsArchiveDownloader = null;
        AzureApi azureApi = null;
        AwsApi awsApi = null;

        if (args.GithubOrg.HasValue())
        {
            var githubApiFactory = sp.GetRequiredService<GithubApiFactory>();
            githubApi = githubApiFactory.Create(null, args.GithubPat);
        }

        if (args.BbsServerUrl.HasValue())
        {
            var bbsApiFactory = sp.GetRequiredService<BbsApiFactory>();

            bbsApi = args.Kerberos
                ? bbsApiFactory.CreateKerberos(args.BbsServerUrl, args.NoSslVerify)
                : bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername, args.BbsPassword, args.NoSslVerify);
        }

        if (args.SshUser.HasValue() || args.SmbUser.HasValue())
        {
            var bbsArchiveDownloaderFactory = sp.GetRequiredService<BbsArchiveDownloaderFactory>();
            var bbsHost = args.ArchiveDownloadHost.HasValue() ? args.ArchiveDownloadHost : new Uri(args.BbsServerUrl).Host;

            bbsArchiveDownloader = args.SshUser.HasValue()
                ? bbsArchiveDownloaderFactory.CreateSshDownloader(bbsHost, args.SshUser, args.SshPrivateKey, args.SshPort, args.BbsSharedHome)
                : bbsArchiveDownloaderFactory.CreateSmbDownloader(bbsHost, args.SmbUser, args.SmbPassword, args.SmbDomain, args.BbsSharedHome);
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

        return new MigrateRepoCommandHandler(log, githubApi, bbsApi, environmentVariableProvider, bbsArchiveDownloader, azureApi, awsApi, fileSystemProvider);
    }
}

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
