using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Commands.GenerateScript;

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

        _log.LogInformation("Generating Script...");

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

        content.AppendLine(VALIDATE_GH_PAT);
        if (!args.Kerberos)
        {
            content.AppendLine(VALIDATE_BBS_PASSWORD);
        }
        if (args.BbsUsername.IsNullOrWhiteSpace() && !args.Kerberos)
        {
            content.AppendLine(VALIDATE_BBS_USERNAME);
        }
        if (args.AwsBucketName.HasValue() || args.AwsRegion.HasValue())
        {
            content.AppendLine(VALIDATE_AWS_ACCESS_KEY_ID);
            content.AppendLine(VALIDATE_AWS_SECRET_ACCESS_KEY);
        }
        else if (!args.UseGithubStorage)
        {
            content.AppendLine(VALIDATE_AZURE_STORAGE_CONNECTION_STRING);
        }
        if (args.SmbUser.HasValue())
        {
            content.AppendLine(VALIDATE_SMB_PASSWORD);
        }

        var projects = args.BbsProject.HasValue()
            ? [args.BbsProject]
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
        var waitOption = wait ? "" : " --queue-only";
        var kerberosOption = args.Kerberos ? " --kerberos" : "";
        var verboseOption = args.Verbose ? " --verbose" : "";
        var sshArchiveDownloadOptions = args.SshUser.HasValue()
            ? $" --ssh-user \"{args.SshUser}\" --ssh-private-key \"{args.SshPrivateKey}\"{(args.SshPort.HasValue() ? $" --ssh-port {args.SshPort}" : "")}{(args.ArchiveDownloadHost.HasValue() ? $" --archive-download-host {args.ArchiveDownloadHost}" : "")}" : "";
        var smbArchiveDownloadOptions = args.SmbUser.HasValue()
            ? $" --smb-user \"{args.SmbUser}\"{(args.SmbDomain.HasValue() ? $" --smb-domain {args.SmbDomain}" : "")}{(args.ArchiveDownloadHost.HasValue() ? $" --archive-download-host {args.ArchiveDownloadHost}" : "")}"
            : "";
        var bbsSharedHomeOption = args.BbsSharedHome.HasValue() ? $" --bbs-shared-home \"{args.BbsSharedHome}\"" : "";
        var awsBucketNameOption = args.AwsBucketName.HasValue() ? $" --aws-bucket-name \"{args.AwsBucketName}\"" : "";
        var awsRegionOption = args.AwsRegion.HasValue() ? $" --aws-region \"{args.AwsRegion}\"" : "";
        var keepArchive = args.KeepArchive ? " --keep-archive" : "";
        var noSslVerify = args.NoSslVerify ? " --no-ssl-verify" : "";
        var targetRepoVisibility = " --target-repo-visibility private";
        var targetApiUrlOption = args.TargetApiUrl.HasValue() ? $" --target-api-url \"{args.TargetApiUrl}\"" : "";
        var targetUploadsUrlOption = args.TargetUploadsUrl.HasValue() ? $" --target-uploads-url \"{args.TargetUploadsUrl}\"" : "";
        var githubStorageOption = args.UseGithubStorage ? " --use-github-storage" : "";

        return $"gh bbs2gh migrate-repo{targetApiUrlOption}{targetUploadsUrlOption}{bbsServerUrlOption}{bbsUsernameOption}{bbsSharedHomeOption}{bbsProjectOption}{bbsRepoOption}{sshArchiveDownloadOptions}" +
               $"{smbArchiveDownloadOptions}{githubOrgOption}{githubRepoOption}{verboseOption}{waitOption}{kerberosOption}{awsBucketNameOption}{awsRegionOption}{keepArchive}{noSslVerify}{targetRepoVisibility}{githubStorageOption}";
    }

    private string Exec(string script) => Wrap(script, "Exec");

    private string Wrap(string script, string outerCommand = "") => script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

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
    private const string VALIDATE_GH_PAT = @"
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}";
    private const string VALIDATE_BBS_USERNAME = @"
if (-not $env:BBS_USERNAME) {
    Write-Error ""BBS_USERNAME environment variable must be set to a valid user that will be used to call Bitbucket Server/Data Center API's to generate a migration archive.""
    exit 1
} else {
    Write-Host ""BBS_USERNAME environment variable is set and will be used to authenticate to Bitbucket Server/Data Center APIs.""
}";
    private const string VALIDATE_BBS_PASSWORD = @"
if (-not $env:BBS_PASSWORD) {
    Write-Error ""BBS_PASSWORD environment variable must be set to a valid password that will be used to call Bitbucket Server/Data Center API's to generate a migration archive.""
    exit 1
} else {
    Write-Host ""BBS_PASSWORD environment variable is set and will be used to authenticate to Bitbucket Server/Data Center APIs.""
}";
    private const string VALIDATE_AZURE_STORAGE_CONNECTION_STRING = @"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";
    private const string VALIDATE_AWS_ACCESS_KEY_ID = @"
if (-not $env:AWS_ACCESS_KEY_ID) {
    Write-Error ""AWS_ACCESS_KEY_ID environment variable must be set to a valid AWS Access Key ID that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_ACCESS_KEY_ID environment variable is set and will be used to upload the migration archive to AWS S3.""
}";
    private const string VALIDATE_AWS_SECRET_ACCESS_KEY = @"
if (-not $env:AWS_SECRET_ACCESS_KEY) {
    Write-Error ""AWS_SECRET_ACCESS_KEY environment variable must be set to a valid AWS Secret Access Key that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_SECRET_ACCESS_KEY environment variable is set and will be used to upload the migration archive to AWS S3.""
}";
    private const string VALIDATE_SMB_PASSWORD = @"
if (-not $env:SMB_PASSWORD) {
    Write-Error ""SMB_PASSWORD environment variable must be set to a valid password that will be used to download the migration archive from your BBS server using SMB.""
    exit 1
} else {
    Write-Host ""SMB_PASSWORD environment variable is set and will be used to download the migration archive from your BBS server using SMB.""
}";
}
