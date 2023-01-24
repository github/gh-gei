using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.BbsToGithub.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.BbsToGithub.Handlers;

public class GenerateScriptCommandHandler : ICommandHandler<GenerateScriptCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly IVersionProvider _versionProvider;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly BbsApi _bbsApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public GenerateScriptCommandHandler(
        OctoLogger log,
        IVersionProvider versionProvider,
        FileSystemProvider fileSystemProvider,
        BbsApi bbsApi,
        EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _versionProvider = versionProvider;
        _fileSystemProvider = fileSystemProvider;
        _bbsApi = bbsApi;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public async Task Handle(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Generating Script...");

        LogOptions(args);

        _log.RegisterSecret(args.BbsPassword);

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

        var projects = args.BbsProjectKey.HasValue()
            ? new List<string>() { args.BbsProjectKey }
            : (await _bbsApi.GetProjects()).Select(x => x.Key);

        foreach (var projectKey in projects)
        {
            _log.LogInformation($"Project: {projectKey}");

            content.AppendLine();
            content.AppendLine($"# =========== Project: {projectKey} ===========");

            var repos = await _bbsApi.GetRepos(projectKey);

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
        var kerberosOption = args.Kerberos ? " --kerberos" : "";
        var verboseOption = args.Verbose ? " --verbose" : "";
        var archiveDownloadOptions = args.SshUser.HasValue()
            ? $" --ssh-user \"{args.SshUser}\" --ssh-private-key \"{args.SshPrivateKey}\"{(args.SshPort.HasValue() ? $" --ssh-port {args.SshPort}" : "")}"
            : "";
        var bbsSharedHomeOption = args.BbsSharedHome.HasValue() ? $" --bbs-shared-home \"{args.BbsSharedHome}\"" : "";
        var awsBucketNameOption = args.AwsBucketName.HasValue() ? $" --aws-bucket-name \"{args.AwsBucketName}\"" : "";

        return $"gh bbs2gh migrate-repo{bbsServerUrlOption}{bbsUsernameOption}{bbsSharedHomeOption}{bbsProjectOption}{bbsRepoOption}{archiveDownloadOptions}{githubOrgOption}{githubRepoOption}{verboseOption}{waitOption}{kerberosOption}{awsBucketNameOption}";
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

        if (args.BbsPassword.HasValue())
        {
            _log.LogInformation("BBS PASSWORD: ***");
        }

        if (args.BbsProjectKey.HasValue())
        {
            _log.LogInformation($"BBS PROJECT KEY: {args.BbsProjectKey}");
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

        if (args.AwsBucketName.HasValue())
        {
            _log.LogInformation($"AWS BUCKET NAME: {args.AwsBucketName}");
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
