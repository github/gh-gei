﻿using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.BbsToGithub.Handlers;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class MigrateRepoCommand : CommandBase<MigrateRepoCommandArgs, MigrateRepoCommandHandler>
{
    public MigrateRepoCommand() : base(
        "migrate-repo",
        "Import a Bitbucket Server archive to GitHub." +
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
        AddOption(SshUser);
        AddOption(SshPrivateKey);
        AddOption(SshPort);
        AddOption(SmbUser);
        AddOption(SmbPassword);
        AddOption(ArchivePath);
        AddOption(AzureStorageConnectionString);
        AddOption(Wait);
        AddOption(Verbose);
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

    public Option<string> ArchiveUrl { get; } = new(
        name: "--archive-url",
        description:
        "URL used to download Bitbucket Server migration archive. Only needed if you want to manually retrieve the archive from BBS instead of letting this CLI do that for you.");

    public Option<string> ArchivePath { get; } = new(
        name: "--archive-path",
        description: "Path to Bitbucket Server migration archive on disk.");

    public Option<string> AzureStorageConnectionString { get; } = new(
        name: "--azure-storage-connection-string",
        description: "A connection string for an Azure Storage account, used to upload the BBS archive.");

    public Option<string> GithubOrg { get; } = new("--github-org");

    public Option<string> GithubRepo { get; } = new("--github-repo");

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
        description: "The SMB user to be used for downloading the export archive off of the Bitbucket server.")
    { IsHidden = true };

    public Option<string> SmbPassword { get; } = new(
        name: "--smb-password",
        description: "The SMB password to be used for downloading the export archive off of the Bitbucket server.")
    { IsHidden = true };

    public Option<string> GithubPat { get; } = new("--github-pat");

    public Option<bool> Wait { get; } = new(
        name: "--wait",
        description: "Synchronously waits for the repo migration to finish.");

    public Option<bool> Verbose { get; } = new("--verbose");

    public override MigrateRepoCommandHandler BuildHandler(MigrateRepoCommandArgs args, ServiceProvider sp)
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

        var githubApiFactory = sp.GetRequiredService<GithubApiFactory>();
        var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
        var githubApi = githubApiFactory.Create(null, args.GithubPat ?? environmentVariableProvider.GithubPersonalAccessToken());

        var bbsApiFactory = sp.GetRequiredService<BbsApiFactory>();
        var bbsApi = bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername ?? environmentVariableProvider.BbsUsername(),
            args.BbsPassword ?? environmentVariableProvider.BbsPassword());

        var bbsArchiveDownloaderFactory = sp.GetRequiredService<BbsArchiveDownloaderFactory>();
        var bbsHost = new Uri(args.BbsServerUrl).Host;
        var bbsArchiveDownloader = bbsArchiveDownloaderFactory.CreateSshDownloader(bbsHost, args.SshUser, args.SshPrivateKey, args.SshPort);

        var azureApiFactory = sp.GetRequiredService<AzureApiFactory>();
        var azureApi = azureApiFactory.Create(args.AzureStorageConnectionString ?? environmentVariableProvider.AzureStorageConnectionString());

        var fileSystemProvider = sp.GetRequiredService<FileSystemProvider>();

        return new MigrateRepoCommandHandler(log, githubApi, bbsApi, bbsArchiveDownloader, azureApi, environmentVariableProvider, fileSystemProvider);
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
