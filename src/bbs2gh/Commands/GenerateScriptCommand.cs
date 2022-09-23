using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using OctoshiftCLI.BbsToGithub.Handlers;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class GenerateScriptCommand : Command
{
    public GenerateScriptCommand(
        OctoLogger log,
        IVersionProvider versionProvider,
        FileSystemProvider fileSystemProvider,
        BbsApiFactory bbsApiFactory,
        EnvironmentVariableProvider environmentVariableProvider) : base(
            name: "generate-script",
            description: "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.")
    {
        var bbsServerUrl = new Option<string>("--bbs-server-url")
        {
            IsRequired = true,
            Description = "The full URL of the Bitbucket Server/Data Center to migrate from."
        };
        var githubOrg = new Option<string>("--github-org") { IsRequired = true };
        var bbsUsername = new Option<string>("--bbs-username")
        {
            IsRequired = false,
            Description = "The Bitbucket username of a user with site admin privileges to get the list of all projects and their repos. If not set will be read from BBS_USERNAME environment variable."
        };
        var bbsPassword = new Option<string>("--bbs-password")
        {
            IsRequired = false,
            Description = "The Bitbucket password of the user specified by --bbs-username to get the list of all projects and their repos. If not set will be read from BBS_PASSWORD environment variable." +
                          $"{Environment.NewLine}" +
                          "Note: The password will not get included in the generated script and it has to be set as an env variable before running the script."
        };
        var sshUser = new Option<string>("--ssh-user")
        {
            IsRequired = true,
            Description = "The SSH user to be used for downloading the export archive off of the Bitbucket server."
        };
        var sshPrivateKey = new Option<string>("--ssh-private-key")
        {
            IsRequired = true,
            Description = "The full path of the private key file to be used for downloading the export archive off of the Bitbucket Server using SSH/SFTP."
        };
        var sshPort = new Option<int>("--ssh-port")
        {
            IsRequired = false,
            Description = "The SSH port (default: 22)."
        };

        var output = new Option<FileInfo>("--output", () => new FileInfo("./migrate.ps1")) { IsRequired = false };
        var verbose = new Option<bool>("--verbose") { IsRequired = false };

        AddOption(bbsServerUrl);
        AddOption(githubOrg);
        AddOption(bbsUsername);
        AddOption(bbsPassword);
        AddOption(sshUser);
        AddOption(sshPrivateKey);
        AddOption(sshPort);
        AddOption(output);
        AddOption(verbose);

        var handler = new GenerateScriptCommandHandler(
            log,
            versionProvider,
            fileSystemProvider,
            bbsApiFactory,
            environmentVariableProvider);
        Handler = CommandHandler.Create<GenerateScriptCommandArgs>(handler.Invoke);
    }
}

public class GenerateScriptCommandArgs
{
    public string BbsServerUrl { get; set; }
    public string GithubOrg { get; set; }
    public string BbsUsername { get; set; }
    public string BbsPassword { get; set; }
    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public string SshPort { get; set; }
    public FileInfo Output { get; set; }
    public bool Verbose { get; set; }
}
