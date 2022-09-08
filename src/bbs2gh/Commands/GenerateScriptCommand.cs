using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.Commands;

public class GenerateScriptCommand : Command
{
    private readonly OctoLogger _log;
    private readonly IVersionProvider _versionProvider;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly BbsApiFactory _bbsApiFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public GenerateScriptCommand(
        OctoLogger log,
        IVersionProvider versionProvider,
        FileSystemProvider fileSystemProvider,
        BbsApiFactory bbsApiFactory,
        EnvironmentVariableProvider environmentVariableProvider) : base("generate-script")
    {
        _log = log;
        _versionProvider = versionProvider;
        _fileSystemProvider = fileSystemProvider;
        _bbsApiFactory = bbsApiFactory;
        _environmentVariableProvider = environmentVariableProvider;

        Description = "Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally modify the script if desired before running it.";

        var bbsServerUrl = new Option<string>("--bbs-server-url")
        {
            IsRequired = true,
            Description = "The full URL of the Bitbucket Server/Data Center to migrate from."
        };
        var githubOrg = new Option<string>("--github-org") { IsRequired = true };
        var bbsUsername = new Option<string>("--bbs-username")
        {
            IsRequired = false,
            Description = "The Bitbucket username of a user with site admin privileges. If not set will be read from BBS_USERNAME environment variable."
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
        AddOption(sshUser);
        AddOption(sshPrivateKey);
        AddOption(sshPort);
        AddOption(output);
        AddOption(verbose);

        Handler = CommandHandler.Create<GenerateScriptCommandArgs>(Invoke);
    }

    public async Task Invoke(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Generating Script...");

        LogOptions(args);

        var script = await GenerateScript(args);

        if (script.HasValue() && args.Output.HasValue())
        {
            await _fileSystemProvider.WriteAllTextAsync(args.Output.FullName, script);
        }
    }

    private async Task<string> GenerateScript(GenerateScriptCommandArgs args)
    {
        var content = new StringBuilder();
        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_FUNCTION_BLOCK);

        var bbsApi = _bbsApiFactory.Create(args.BbsServerUrl, args.BbsUsername, _environmentVariableProvider.BbsPassword());
        var projects = await bbsApi.GetProjects();
        foreach (var (_, projectKey, projectName) in projects)
        {
            _log.LogInformation($"Project: {projectName}");
            
            content.AppendLine();
            content.AppendLine($"# =========== Project: {projectName} ===========");

            var repos = await bbsApi.GetRepos(projectKey);

            if (!repos.Any())
            {
                content.AppendLine("# Skipping this project because it has no git repos.");
                continue;
            }

            content.AppendLine();

            foreach (var (_, repoSlug, repoName) in repos)
            {
                _log.LogInformation($"  Repo: {repoName}");
                
                content.AppendLine(Exec(MigrateGithubRepoScript(args, projectKey, repoSlug, true)));
            }
        }

        return content.ToString();
    }

    private string MigrateGithubRepoScript(GenerateScriptCommandArgs args, string bbsProjectKey, string bbsRepoSlug, bool wait)
    {
        var bbsServerUrlOption = $" --bbs-server-url \"{args.BbsServerUrl}\"";
        var bbsUsernameOption = args.BbsUsername.HasValue() ? $" --bbs-username \"{args.BbsUsername}\"" : "";
        var bbsProjectOption = $" --bbs-project \"{bbsProjectKey}\"";
        var bbsRepoOption = $" --bbs-repo \"{bbsRepoSlug}\"";
        var githubOrgOption = $" --github-org \"{args.GithubOrg}\"";
        var githubRepoOption = $" --github-repo \"{GetGithubRepoName(bbsProjectKey, bbsRepoSlug)}\"";
        var waitOption = wait ? " --wait" : "";
        var verboseOption = args.Verbose ? " --verbose" : "";
        var archiveDownloadOptions = args.SshUser.HasValue()
            ? $" --ssh-user \"{args.SshUser}\" --ssh-private-key \"{args.SshPrivateKey}\"{(args.SshPort.HasValue() ? $" --ssh-port {args.SshPort}" : "")}"
            : "";

        return $"gh bbs2gh migrate-repo{bbsServerUrlOption}{bbsUsernameOption}{bbsProjectOption}{bbsRepoOption}{archiveDownloadOptions}{githubOrgOption}{githubRepoOption}{verboseOption}{waitOption}";
    }

    private string Exec(string script) => Wrap(script, "Exec");

    private string Wrap(string script, string outerCommand = "") => script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

    private void LogOptions(GenerateScriptCommandArgs args)
    {
        if (args.BbsServerUrl.HasValue())
        {
            _log.LogInformation($"BBS SERVER URL: {args.BbsServerUrl}");
        }

        if (args.GithubOrg.HasValue())
        {
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        }

        if (args.BbsUsername.HasValue())
        {
            _log.LogInformation($"BBS USERNAME: {args.BbsUsername}");
        }

        if (args.SshUser.HasValue())
        {
            _log.LogInformation($"SSH USER: {args.SshUser}");
        }

        if (args.SshPrivateKey.HasValue())
        {
            _log.LogInformation($"SSH PRIVATE KEY: {args.SshPrivateKey}");
        }

        if (args.SshPort.HasValue())
        {
            _log.LogInformation($"SSH PORT: {args.SshPort}");
        }

        if (args.Output.HasValue())
        {
            _log.LogInformation($"OUTPUT: {args.Output}");
        }
    }

    private string GetGithubRepoName(string bbsProjectKey, string bbsRepoSlug) => $"{bbsProjectKey}-{bbsRepoSlug}".ReplaceInvalidCharactersWithDash();

    private string VersionComment => $"# =========== Created with CLI version {_versionProvider.GetCurrentVersion()} ===========";

    private const string PWSH_SHEBANG = "#!/usr/bin/env pwsh";

    private const string EXEC_FUNCTION_BLOCK = @"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}";
}

public class GenerateScriptCommandArgs
{
    public string BbsServerUrl { get; set; }
    public string GithubOrg { get; set; }
    public string BbsUsername { get; set; }
    public string SshUser { get; set; }
    public string SshPrivateKey { get; set; }
    public string SshPort { get; set; }
    public FileInfo Output { get; set; }
    public bool Verbose { get; set; }
}
