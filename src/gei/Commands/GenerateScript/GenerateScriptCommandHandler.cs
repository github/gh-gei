using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript;

public class GenerateScriptCommandHandler : ICommandHandler<GenerateScriptCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly GithubApi _sourceGithubApi;
    private readonly IVersionProvider _versionProvider;
    private readonly GhesVersionChecker _ghesVersionChecker;

    public GenerateScriptCommandHandler(
        OctoLogger log,
        GithubApi sourceGithubApi,
        IVersionProvider versionProvider,
        GhesVersionChecker ghesVersionChecker)
    {
        _log = log;
        _sourceGithubApi = sourceGithubApi;
        _versionProvider = versionProvider;
        _ghesVersionChecker = ghesVersionChecker;
    }

    public async Task Handle(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Generating Script...");

        var script = await GenerateScript(args.GithubSourceOrg, args.GithubTargetOrg, args.GhesApiUrl, args.AwsBucketName, args.AwsRegion, args.NoSslVerify, args.Sequential, args.SkipReleases, args.LockSourceRepo, args.DownloadMigrationLogs, args.KeepArchive, args.TargetApiUrl, args.TargetUploadsUrl, args.UseGithubStorage);

        if (script.HasValue() && args.Output.HasValue())
        {
            await WriteToFile(args.Output.FullName, script);
        }
    }

    private async Task<string> GenerateScript(string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool sequential, bool skipReleases, bool lockSourceRepo, bool downloadMigrationLogs, bool keepArchive, string targetApiUrl, string targetUploadsUrl, bool useGithubStorage)
    {
        var repos = await GetGithubRepos(_sourceGithubApi, githubSourceOrg);
        if (!repos.Any())
        {
            _log.LogError("A migration script could not be generated because no migratable repos were found.");
            return string.Empty;
        }

        return sequential
            ? await GenerateSequentialGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, skipReleases, lockSourceRepo, downloadMigrationLogs, keepArchive, targetApiUrl, targetUploadsUrl, useGithubStorage)
            : await GenerateParallelGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, skipReleases, lockSourceRepo, downloadMigrationLogs, keepArchive, targetApiUrl, targetUploadsUrl, useGithubStorage);
    }

    private async Task<IEnumerable<(string Name, string Visibility)>> GetGithubRepos(GithubApi github, string githubOrg)
    {
        if (githubOrg.IsNullOrWhiteSpace() || github is null)
        {
            throw new ArgumentException("All arguments must be non-null");
        }

        _log.LogInformation($"GITHUB ORG: {githubOrg}");
        var repos = await github.GetRepos(githubOrg);
        foreach (var (name, _) in repos)
        {
            _log.LogInformation($"    Repo: {name}");
        }

        return repos;
    }

    private async Task<string> GenerateSequentialGithubScript(IEnumerable<(string Name, string Visibility)> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool skipReleases, bool lockSourceRepo, bool downloadMigrationLogs, bool keepArchive, string targetApiUrl, string targetUploadsUrl, bool useGithubStorage)
    {
        var content = new StringBuilder();

        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_FUNCTION_BLOCK);

        content.AppendLine(VALIDATE_GH_PAT);

        if (!useGithubStorage && await _ghesVersionChecker.AreBlobCredentialsRequired(ghesApiUrl))
        {
            if (awsBucketName.HasValue() || awsRegion.HasValue())
            {
                content.AppendLine(VALIDATE_AWS_ACCESS_KEY_ID);
                content.AppendLine(VALIDATE_AWS_SECRET_ACCESS_KEY);
            }
            else
            {
                content.AppendLine(VALIDATE_AZURE_STORAGE_CONNECTION_STRING);
            }
        }

        content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

        foreach (var (name, visibility) in repos)
        {
            content.AppendLine(Exec(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, name, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, true, skipReleases, lockSourceRepo, keepArchive, visibility, targetApiUrl, targetUploadsUrl, useGithubStorage)));

            if (downloadMigrationLogs)
            {
                content.AppendLine(Exec(DownloadMigrationLogScript(githubTargetOrg, name, targetApiUrl)));
            }
        }

        return content.ToString();
    }

    private async Task<string> GenerateParallelGithubScript(IEnumerable<(string Name, string Visibility)> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool skipReleases, bool lockSourceRepo, bool downloadMigrationLogs, bool keepArchive, string targetApiUrl, string targetUploadsUrl, bool useGithubStorage)
    {
        var content = new StringBuilder();

        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);

        content.AppendLine(VALIDATE_GH_PAT);
        if (!useGithubStorage && await _ghesVersionChecker.AreBlobCredentialsRequired(ghesApiUrl))
        {
            if (awsBucketName.HasValue() || awsRegion.HasValue())
            {
                content.AppendLine(VALIDATE_AWS_ACCESS_KEY_ID);
                content.AppendLine(VALIDATE_AWS_SECRET_ACCESS_KEY);
            }
            else
            {
                content.AppendLine(VALIDATE_AZURE_STORAGE_CONNECTION_STRING);
            }
        }

        content.AppendLine();
        content.AppendLine("$Succeeded = 0");
        content.AppendLine("$Failed = 0");
        content.AppendLine("$RepoMigrations = [ordered]@{}");

        content.AppendLine();
        content.AppendLine($"# =========== Organization: {githubSourceOrg} ===========");

        content.AppendLine();
        content.AppendLine("# === Queuing repo migrations ===");

        // Queuing migrations
        foreach (var (name, visibility) in repos)
        {
            content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, name, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, false, skipReleases, lockSourceRepo, keepArchive, visibility, targetApiUrl, targetUploadsUrl, useGithubStorage))}");
            content.AppendLine($"$RepoMigrations[\"{name}\"] = $MigrationID");
            content.AppendLine();
        }

        // Waiting for migrations
        content.AppendLine();
        content.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {githubSourceOrg} ===========");
        content.AppendLine();

        // Query each migration's status
        foreach (var (name, _) in repos)
        {
            content.AppendLine(Wrap(WaitForMigrationScript(targetApiUrl, name), $"if ($RepoMigrations[\"{name}\"])"));
            content.AppendLine($"if ($RepoMigrations[\"{name}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");

            if (downloadMigrationLogs)
            {
                content.AppendLine(DownloadMigrationLogScript(githubTargetOrg, name, targetApiUrl));
            }

            content.AppendLine();
        }

        // Generating the final report
        content.AppendLine();
        content.AppendLine("Write-Host =============== Summary ===============");
        content.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
        content.AppendLine("Write-Host Total number of failed migrations: $Failed");

        content.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");

        content.AppendLine();
        content.AppendLine();

        return content.ToString();
    }

    private string MigrateGithubRepoScript(string githubSourceOrg, string githubTargetOrg, string repo, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool wait, bool skipReleases, bool lockSourceRepo, bool keepArchive, string repoVisibility, string targetApiUrl, string targetUploadsUrl, bool useGithubStorage)
    {
        var ghesRepoOptions = ghesApiUrl.HasValue() ? GetGhesRepoOptions(ghesApiUrl, awsBucketName, awsRegion, noSslVerify, keepArchive, useGithubStorage) : null;

        return $"gh gei migrate-repo{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)}{(targetUploadsUrl.HasValue() ? $" --target-uploads-url \"{targetUploadsUrl}\"" : string.Empty)} --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(!string.IsNullOrEmpty(ghesRepoOptions) ? $" {ghesRepoOptions}" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? string.Empty : " --queue-only")}{(skipReleases ? " --skip-releases" : string.Empty)}{(lockSourceRepo ? " --lock-source-repo" : string.Empty)} --target-repo-visibility {repoVisibility}";
    }

    private string GetGhesRepoOptions(string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool keepArchive, bool useGithubStorage)
    {
        return $"--ghes-api-url \"{ghesApiUrl}\"{(awsBucketName.HasValue() ? $" --aws-bucket-name \"{awsBucketName}\"" : "")}{(awsRegion.HasValue() ? $" --aws-region \"{awsRegion}\"" : "")}{(noSslVerify ? " --no-ssl-verify" : string.Empty)}{(keepArchive ? " --keep-archive" : string.Empty)}{(useGithubStorage ? " --use-github-storage" : string.Empty)}";
    }

    private string WaitForMigrationScript(string targetApiUrl, string repoMigrationKey = null) => $"gh gei wait-for-migration{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

    private string DownloadMigrationLogScript(string githubTargetOrg, string targetRepo, string targetApiUrl)
    {
        return $"gh gei download-logs{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --github-target-org \"{githubTargetOrg}\" --target-repo \"{targetRepo}\"";
    }

    private string Exec(string script) => Wrap(script, "Exec");

    private string ExecAndGetMigrationId(string script) => Wrap(script, "ExecAndGetMigrationID");

    private string Wrap(string script, string outerCommand = "") =>
        script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();

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

    private const string EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK = @"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}";
    private const string VALIDATE_GH_PAT = @"
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
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
