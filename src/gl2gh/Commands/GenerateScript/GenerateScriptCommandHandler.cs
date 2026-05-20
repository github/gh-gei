using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.GenerateScript;

public class GenerateScriptCommandHandler : ICommandHandler<GenerateScriptCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly IVersionProvider _versionProvider;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly GitlabApi _gitlabApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public GenerateScriptCommandHandler(
        OctoLogger log,
        IVersionProvider versionProvider,
        FileSystemProvider fileSystemProvider,
        GitlabApi gitlabApi,
        EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _versionProvider = versionProvider;
        _fileSystemProvider = fileSystemProvider;
        _gitlabApi = gitlabApi;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public async Task Handle(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Generating Script...");

        await _gitlabApi.LogServerVersion();

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
        content.AppendLine(VALIDATE_GITLAB_PAT);
        if (args.AwsBucketName.HasValue() || args.AwsRegion.HasValue())
        {
            content.AppendLine(VALIDATE_AWS_ACCESS_KEY_ID);
            content.AppendLine(VALIDATE_AWS_SECRET_ACCESS_KEY);
        }
        else if (!args.UseGithubStorage)
        {
            content.AppendLine(VALIDATE_AZURE_STORAGE_CONNECTION_STRING);
        }

        var groups = args.GitlabGroup.HasValue()
            ? new[] { args.GitlabGroup }
            : (await _gitlabApi.GetGroups()).Select(x => x.Path);

        foreach (var groupPath in groups)
        {
            _log.LogInformation($"Group: {groupPath}");

            content.AppendLine();
            content.AppendLine($"# =========== Group: {groupPath} ===========");

            var projects = await _gitlabApi.GetProjects(groupPath);

            if (args.GitlabProject.HasValue())
            {
                projects = projects.Where(p => p.Path == args.GitlabProject).ToArray();
            }

            if (!projects.Any())
            {
                content.AppendLine("# Skipping this group because it has no projects.");
                continue;
            }

            content.AppendLine();

            foreach (var (_, projectPath, projectName, _) in projects)
            {
                _log.LogInformation($"  Project: {projectName}");

                content.AppendLine(Exec(MigrateGithubRepoScript(args, groupPath, projectPath, true)));
            }
        }

        return content.ToString();
    }

    private string MigrateGithubRepoScript(GenerateScriptCommandArgs args, string gitlabGroup, string gitlabProject, bool wait)
    {
        var gitlabServerUrlOption = $" --gitlab-server-url \"{args.GitlabServerUrl}\"";
        var gitlabGroupOption = $" --gitlab-group \"{gitlabGroup}\"";
        var gitlabProjectOption = $" --gitlab-project \"{gitlabProject}\"";
        var githubOrgOption = $" --github-org \"{args.GithubOrg}\"";
        var githubRepoOption = $" --github-repo \"{GetGithubRepoName(gitlabGroup, gitlabProject)}\"";
        var waitOption = wait ? "" : " --queue-only";
        var verboseOption = args.Verbose ? " --verbose" : "";
        var awsBucketNameOption = args.AwsBucketName.HasValue() ? $" --aws-bucket-name \"{args.AwsBucketName}\"" : "";
        var awsRegionOption = args.AwsRegion.HasValue() ? $" --aws-region \"{args.AwsRegion}\"" : "";
        var keepArchive = args.KeepArchive ? " --keep-archive" : "";
        var noSslVerifyOption = args.NoSslVerify ? " --no-ssl-verify" : "";
        var targetRepoVisibility = " --target-repo-visibility private";
        var targetApiUrlOption = args.TargetApiUrl.HasValue() ? $" --target-api-url \"{args.TargetApiUrl}\"" : "";
        var targetUploadsUrlOption = args.TargetUploadsUrl.HasValue() ? $" --target-uploads-url \"{args.TargetUploadsUrl}\"" : "";
        var githubStorageOption = args.UseGithubStorage ? " --use-github-storage" : "";

        return $"gh gl2gh migrate-repo{targetApiUrlOption}{targetUploadsUrlOption}{gitlabServerUrlOption}{gitlabGroupOption}{gitlabProjectOption}" +
               $"{githubOrgOption}{githubRepoOption}{verboseOption}{waitOption}{awsBucketNameOption}{awsRegionOption}{keepArchive}{noSslVerifyOption}{targetRepoVisibility}{githubStorageOption}";
    }

    private string Exec(string script) => Wrap(script, "Exec");

    private string Wrap(string script, string outerCommand = "") => script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

    private string GetGithubRepoName(string gitlabGroup, string gitlabProject) => $"{gitlabGroup}-{gitlabProject}".ReplaceInvalidCharactersWithDash();

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
    private const string VALIDATE_GITLAB_PAT = @"
if (-not $env:GITLAB_PAT) {
    Write-Error ""GITLAB_PAT environment variable must be set to a valid PAT that will be used to call the GitLab API to generate a migration archive.""
    exit 1
} else {
    Write-Host ""GITLAB_PAT environment variable is set and will be used to authenticate to the GitLab API.""
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
}
