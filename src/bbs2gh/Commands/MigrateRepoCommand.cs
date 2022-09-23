using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.BbsToGithub.Handlers;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class MigrateRepoCommand : Command
{
    public MigrateRepoCommand(
        OctoLogger log,
        GithubApiFactory githubApiFactory,
        BbsApiFactory bbsApiFactory,
        EnvironmentVariableProvider environmentVariableProvider,
        BbsArchiveDownloaderFactory bbsArchiveDownloaderFactory,
        IAzureApiFactory azureApiFactory,
        FileSystemProvider fileSystemProvider) : base(
            name: "migrate-repo",
            description: "Import a Bitbucket Server archive to GitHub." +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
    {
        // Arguments to generate a new archive
        var bbsServerUrl = new Option<string>("--bbs-server-url")
        {
            IsRequired = false,
            Description = "The full URL of the Bitbucket Server/Data Center to migrate from. E.g. http://bitbucket.contoso.com:7990"
        };

        var bbsProject = new Option<string>("--bbs-project")
        {
            IsRequired = false,
            Description = "The Bitbucket project to migrate."
        };

        var bbsRepo = new Option<string>("--bbs-repo")
        {
            IsRequired = false,
            Description = "The Bitbucket repository to migrate."
        };

        var bbsUsername = new Option<string>("--bbs-username")
        {
            IsRequired = false,
            Description = "The Bitbucket username of a user with site admin privileges. If not set will be read from BBS_USERNAME environment variable."
        };

        var bbsPassword = new Option<string>("--bbs-password")
        {
            IsRequired = false,
            Description = "The Bitbucket password of the user specified by --bbs-username. If not set will be read from BBS_PASSWORD environment variable."
        };

        // Arguments to import an existing archive
        var archiveUrl = new Option<string>("--archive-url")
        {
            IsRequired = false,
            Description = "URL used to download Bitbucket Server migration archive. Only needed if you want to manually retrieve the archive from BBS instead of letting this CLI do that for you."
        };

        var archivePath = new Option<string>("--archive-path")
        {
            IsRequired = false,
            Description = "Path to Bitbucket Server migration archive on disk."
        };

        var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
        {
            IsRequired = false,
            Description = "A connection string for an Azure Storage account, used to upload the BBS archive."
        };

        var githubOrg = new Option<string>("--github-org")
        {
            IsRequired = false
        };
        var githubRepo = new Option<string>("--github-repo")
        {
            IsRequired = false
        };

        var sshUser = new Option<string>("--ssh-user")
        {
            IsRequired = false,
            Description = "The SSH user to be used for downloading the export archive off of the Bitbucket server."
        };
        var sshPrivateKey = new Option<string>("--ssh-private-key")
        {
            IsRequired = false,
            Description = "The full path of the private key file to be used for downloading the export archive off of the Bitbucket Server using SSH/SFTP." +
                          Environment.NewLine +
                          "Supported private key formats:" +
                          Environment.NewLine +
                          "  - RSA in OpenSSL PEM format." +
                          Environment.NewLine +
                          "  - DSA in OpenSSL PEM format." +
                          Environment.NewLine +
                          "  - ECDSA 256/384/521 in OpenSSL PEM format." +
                          Environment.NewLine +
                          "  - ECDSA 256/384/521, ED25519 and RSA in OpenSSH key format."
        };
        var sshPort = new Option<int>("--ssh-port")
        {
            IsRequired = false,
            Description = "The SSH port (default: 22)."
        };

        var smbUser = new Option<string>("--smb-user")
        {
            IsRequired = false,
            IsHidden = true,
            Description = "The SMB user to be used for downloading the export archive off of the Bitbucket server."
        };
        var smbPassword = new Option<string>("--smb-password")
        {
            IsRequired = false,
            IsHidden = true,
            Description = "The SMB password to be used for downloading the export archive off of the Bitbucket server."
        };

        var githubPat = new Option<string>("--github-pat")
        {
            IsRequired = false
        };
        var wait = new Option<bool>("--wait")
        {
            Description = "Synchronously waits for the repo migration to finish."
        };
        var verbose = new Option<bool>("--verbose")
        {
            IsRequired = false
        };

        AddOption(archiveUrl);
        AddOption(githubOrg);
        AddOption(githubRepo);
        AddOption(githubPat);

        AddOption(bbsServerUrl);
        AddOption(bbsProject);
        AddOption(bbsRepo);
        AddOption(bbsUsername);
        AddOption(bbsPassword);

        AddOption(sshUser);
        AddOption(sshPrivateKey);
        AddOption(sshPort);

        AddOption(smbUser);
        AddOption(smbPassword);

        AddOption(archivePath);
        AddOption(azureStorageConnectionString);

        AddOption(wait);
        AddOption(verbose);

        var handler = new MigrateRepoCommandHandler(
            log,
            githubApiFactory,
            bbsApiFactory,
            environmentVariableProvider,
            bbsArchiveDownloaderFactory,
            azureApiFactory,
            fileSystemProvider);
        Handler = CommandHandler.Create<MigrateRepoCommandArgs>(handler.Invoke);
    }
}

public class MigrateRepoCommandArgs
{
    public string ArchiveUrl { get; set; }
    public string ArchivePath { get; set; }

    public string AzureStorageConnectionString { get; set; }

    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string GithubPat { get; set; }
    public bool Wait { get; set; }
    public bool Verbose { get; set; }

    public string BbsServerUrl { get; set; }
    public string BbsProject { get; set; }
    public string BbsRepo { get; set; }
    public string BbsUsername { get; set; }
    public string BbsPassword { get; set; }

    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public int SshPort { get; set; } = 22;

    public string SmbUser { get; set; }
    public string SmbPassword { get; set; }
}
