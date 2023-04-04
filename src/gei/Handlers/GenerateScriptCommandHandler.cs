﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Handlers;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Handlers;

public class GenerateScriptCommandHandler : ICommandHandler<GenerateScriptCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly GithubApi _sourceGithubApi;
    private readonly AdoApi _sourceAdoApi;
    private readonly IVersionProvider _versionProvider;
    private readonly GhesVersionChecker _ghesVersionCheckerService;

    public GenerateScriptCommandHandler(
        OctoLogger log,
        GithubApi sourceGithubApi,
        AdoApi sourceAdoApi,
        IVersionProvider versionProvider,
        GhesVersionChecker ghesVersionCheckerService)
    {
        _log = log;
        _sourceGithubApi = sourceGithubApi;
        _sourceAdoApi = sourceAdoApi;
        _versionProvider = versionProvider;
        _ghesVersionCheckerService = ghesVersionCheckerService;
    }

    public async Task Handle(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.RegisterSecret(args.GithubSourcePat);
        _log.RegisterSecret(args.AdoPat);

        _log.LogInformation("Generating Script...");

        var hasAdoSpecificArg = new[] { args.AdoPat, args.AdoServerUrl, args.AdoSourceOrg, args.AdoTeamProject }.Any(arg => arg.HasValue());
        if (hasAdoSpecificArg)
        {
            _log.LogWarning("ADO migration feature will be removed from `gh gei` in near future, please consider switching to `gh ado2gh` for ADO migrations instead.");
        }

        LogArgs(args);
        ValidateArgs(args);

        var script = args.GithubSourceOrg.IsNullOrWhiteSpace() ?
            await InvokeAdo(args.AdoServerUrl, args.AdoSourceOrg, args.AdoTeamProject, args.GithubTargetOrg, args.Sequential, args.DownloadMigrationLogs) :
            await InvokeGithub(args.GithubSourceOrg, args.GithubTargetOrg, args.GhesApiUrl, args.AwsBucketName, args.AwsRegion, args.NoSslVerify, args.Sequential, args.SkipReleases, args.LockSourceRepo, args.DownloadMigrationLogs, args.KeepArchive);

        if (script.HasValue() && args.Output.HasValue())
        {
            await WriteToFile(args.Output.FullName, script);
        }
    }

    private void ValidateArgs(GenerateScriptCommandArgs args)
    {
        if (args.GithubSourceOrg.IsNullOrWhiteSpace() && args.AdoSourceOrg.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("Must specify either --github-source-org or --ado-source-org");
        }

        if (args.AdoServerUrl.HasValue() && !args.AdoSourceOrg.HasValue())
        {
            throw new OctoshiftCliException("Must specify --ado-source-org with the collection name when using --ado-server-url");
        }

        if (args.AwsBucketName.HasValue() && args.GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url must be specified when --aws-bucket-name is specified.");
        }

        if (args.NoSslVerify && args.GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
        }
    }

    private void LogArgs(GenerateScriptCommandArgs args)
    {
        if (args.GithubSourceOrg.HasValue())
        {
            _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
        }

        if (args.AdoServerUrl.HasValue())
        {
            _log.LogInformation($"ADO SERVER URL: {args.AdoServerUrl}");
        }

        if (args.AdoSourceOrg.HasValue())
        {
            _log.LogInformation($"ADO SOURCE ORG: {args.AdoSourceOrg}");
        }

        if (args.AdoTeamProject.HasValue())
        {
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        }

        if (args.SkipReleases)
        {
            _log.LogInformation("SKIP RELEASES: true");
        }

        if (args.LockSourceRepo)
        {
            _log.LogInformation("LOCK SOURCE REPO: true");
        }

        if (args.DownloadMigrationLogs)
        {
            _log.LogInformation("DOWNLOAD MIGRATION LOGS: true");
        }

        if (args.GithubTargetOrg.HasValue())
        {
            _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");
        }

        if (args.Output.HasValue())
        {
            _log.LogInformation($"OUTPUT: {args.Output}");
        }

        if (args.Sequential)
        {
            _log.LogInformation("SEQUENTIAL: true");
        }

        if (args.GithubSourcePat.HasValue())
        {
            _log.LogInformation("GITHUB SOURCE PAT: ***");
        }

        if (args.AdoPat.HasValue())
        {
            _log.LogInformation("ADO PAT: ***");
        }

        if (args.GhesApiUrl.HasValue())
        {
            _log.LogInformation($"GHES API URL: {args.GhesApiUrl}");
        }

        if (args.NoSslVerify)
        {
            _log.LogInformation("SSL verification disabled");
        }

        if (args.AwsBucketName.HasValue())
        {
            _log.LogInformation($"AWS BUCKET NAME: {args.AwsBucketName}");
        }

        if (args.AwsRegion.HasValue())
        {
            _log.LogInformation($"AWS REGION: {args.AwsRegion}");
        }

        if (args.KeepArchive)
        {
            _log.LogInformation("KEEP ARCHIVE: true");
        }
    }

    private async Task<string> InvokeGithub(string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool sequential, bool skipReleases, bool lockSourceRepo, bool downloadMigrationLogs, bool keepArchive)
    {
        var repos = await GetGithubRepos(_sourceGithubApi, githubSourceOrg);
        if (!repos.Any())
        {
            _log.LogError("A migration script could not be generated because no migratable repos were found.");
            return string.Empty;
        }

        return sequential
            ? await GenerateSequentialGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, skipReleases, lockSourceRepo, downloadMigrationLogs, keepArchive)
            : await GenerateParallelGithubScript(repos, githubSourceOrg, githubTargetOrg, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, skipReleases, lockSourceRepo, downloadMigrationLogs, keepArchive);
    }

    private async Task<string> InvokeAdo(string adoServerUrl, string adoSourceOrg, string adoTeamProject, string githubTargetOrg, bool sequential, bool downloadMigrationLogs)
    {
        var repos = await GetAdoRepos(_sourceAdoApi, adoSourceOrg, adoTeamProject);
        if (!repos.Any())
        {
            _log.LogError("A migration script could not be generated because no migratable repos were found. Please note that the GEI does not migrate disabled or TFVC repos.");
            return string.Empty;
        }

        return sequential
            ? GenerateSequentialAdoScript(repos, adoServerUrl, adoSourceOrg, githubTargetOrg, downloadMigrationLogs)
            : GenerateParallelAdoScript(repos, adoServerUrl, adoSourceOrg, githubTargetOrg, downloadMigrationLogs);
    }

    private async Task<IEnumerable<string>> GetGithubRepos(GithubApi github, string githubOrg)
    {
        if (githubOrg.IsNullOrWhiteSpace() || github is null)
        {
            throw new ArgumentException("All arguments must be non-null");
        }

        _log.LogInformation($"GITHUB ORG: {githubOrg}");
        var repos = await github.GetRepos(githubOrg);

        foreach (var repo in repos)
        {
            _log.LogInformation($"    Repo: {repo}");
        }

        return repos;
    }

    private async Task<IDictionary<string, IEnumerable<string>>> GetAdoRepos(AdoApi adoApi, string adoOrg, string adoTeamProject)
    {
        if (adoOrg.IsNullOrWhiteSpace() || adoApi is null)
        {
            throw new ArgumentException("All arguments must be non-null");
        }

        var repos = new Dictionary<string, IEnumerable<string>>();

        var teamProjects = await adoApi.GetTeamProjects(adoOrg);
        if (adoTeamProject.HasValue())
        {
            teamProjects = teamProjects.Any(o => o.Equals(adoTeamProject, StringComparison.OrdinalIgnoreCase))
                ? new[] { adoTeamProject }
                : Enumerable.Empty<string>();
        }

        foreach (var teamProject in teamProjects)
        {
            var projectRepos = await GetTeamProjectRepos(adoApi, adoOrg, teamProject);
            repos.Add(teamProject, projectRepos);
        }

        return repos;
    }

    private async Task<string> GenerateSequentialGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool skipReleases, bool lockSourceRepo, bool downloadMigrationLogs, bool keepArchive)
    {
        var content = new StringBuilder();

        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_FUNCTION_BLOCK);

        content.AppendLine(VALIDATE_GH_PAT);
        if (await _ghesVersionCheckerService.AreBlobCredentialsRequired(ghesApiUrl, _sourceGithubApi))
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

        foreach (var repo in repos)
        {
            content.AppendLine(Exec(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, true, skipReleases, lockSourceRepo, keepArchive)));

            if (downloadMigrationLogs)
            {
                content.AppendLine(Exec(DownloadMigrationLogScript(githubTargetOrg, repo)));
            }
        }

        return content.ToString();
    }

    private async Task<string> GenerateParallelGithubScript(IEnumerable<string> repos, string githubSourceOrg, string githubTargetOrg, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool skipReleases, bool lockSourceRepo, bool downloadMigrationLogs, bool keepArchive)
    {
        var content = new StringBuilder();

        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);

        content.AppendLine(VALIDATE_GH_PAT);
        if (await _ghesVersionCheckerService.AreBlobCredentialsRequired(ghesApiUrl, _sourceGithubApi))
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
        foreach (var repo in repos)
        {
            content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateGithubRepoScript(githubSourceOrg, githubTargetOrg, repo, ghesApiUrl, awsBucketName, awsRegion, noSslVerify, false, skipReleases, lockSourceRepo, keepArchive))}");
            content.AppendLine($"$RepoMigrations[\"{repo}\"] = $MigrationID");
            content.AppendLine();
        }

        // Waiting for migrations
        content.AppendLine();
        content.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {githubSourceOrg} ===========");
        content.AppendLine();

        // Query each migration's status
        foreach (var repo in repos)
        {
            content.AppendLine(Wrap(WaitForMigrationScript(repo), $"if ($RepoMigrations[\"{repo}\"])"));
            content.AppendLine($"if ($RepoMigrations[\"{repo}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");

            if (downloadMigrationLogs)
            {
                content.AppendLine(DownloadMigrationLogScript(githubTargetOrg, repo));
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

    private string GenerateSequentialAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoServerUrl, string adoSourceOrg, string githubTargetOrg, bool downloadMigrationLogs)
    {
        var content = new StringBuilder();

        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_FUNCTION_BLOCK);

        content.AppendLine($"# =========== Organization: {adoSourceOrg} ===========");

        foreach (var teamProject in repos.Keys)
        {
            content.AppendLine();
            content.AppendLine($"# === Team Project: {adoSourceOrg}/{teamProject} ===");

            if (!repos[teamProject].Any())
            {
                content.AppendLine("# Skipping this Team Project because it has no git repos");
            }
            else
            {
                foreach (var repo in repos[teamProject])
                {
                    var githubRepo = GetGithubRepoName(teamProject, repo);
                    content.AppendLine(Exec(MigrateAdoRepoScript(adoServerUrl, adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, true)));

                    if (downloadMigrationLogs)
                    {
                        content.AppendLine(Exec(DownloadMigrationLogScript(githubTargetOrg, githubRepo)));
                    }
                }
            }
        }

        return content.ToString();
    }

    private string GenerateParallelAdoScript(IDictionary<string, IEnumerable<string>> repos, string adoServerUrl, string adoSourceOrg, string githubTargetOrg, bool downloadMigrationLogs)
    {
        var content = new StringBuilder();

        content.AppendLine(PWSH_SHEBANG);
        content.AppendLine();
        content.AppendLine(VersionComment);
        content.AppendLine(EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);

        content.AppendLine();
        content.AppendLine("$Succeeded = 0");
        content.AppendLine("$Failed = 0");
        content.AppendLine("$RepoMigrations = [ordered]@{}");

        content.AppendLine();
        content.AppendLine($"# =========== Organization: {adoSourceOrg} ===========");

        // Queueing migrations
        foreach (var teamProject in repos.Keys)
        {
            content.AppendLine();
            content.AppendLine($"# === Queuing repo migrations for Team Project: {adoSourceOrg}/{teamProject} ===");

            if (!repos[teamProject].Any())
            {
                content.AppendLine("# Skipping this Team Project because it has no git repos");
                continue;
            }

            foreach (var repo in repos[teamProject])
            {
                var githubRepo = GetGithubRepoName(teamProject, repo);
                content.AppendLine($"$MigrationID = {ExecAndGetMigrationId(MigrateAdoRepoScript(adoServerUrl, adoSourceOrg, teamProject, repo, githubTargetOrg, githubRepo, false))}");
                content.AppendLine($"$RepoMigrations[\"{githubRepo}\"] = $MigrationID");
                content.AppendLine();
            }
        }

        // Waiting for migrations
        content.AppendLine();
        content.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoSourceOrg} ===========");

        // Query each migration's status
        foreach (var teamProject in repos.Keys)
        {
            if (repos[teamProject].Any())
            {
                content.AppendLine();
                content.AppendLine($"# === Migration status for Team Project: {adoSourceOrg}/{teamProject} ===");
            }

            foreach (var repo in repos[teamProject].Select(r => GetGithubRepoName(teamProject, r)))
            {
                content.AppendLine(Wrap(WaitForMigrationScript(repo), $"if ($RepoMigrations[\"{repo}\"])"));
                content.AppendLine($"if ($RepoMigrations[\"{repo}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");

                if (downloadMigrationLogs)
                {
                    content.AppendLine(DownloadMigrationLogScript(githubTargetOrg, repo));
                }

                content.AppendLine();
            }
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

    private async Task<IEnumerable<string>> GetTeamProjectRepos(AdoApi adoApi, string adoOrg, string teamProject)
    {
        _log.LogInformation($"Team Project: {teamProject}");
        var projectRepos = (await adoApi.GetEnabledRepos(adoOrg, teamProject)).Select(repo => repo.Name);

        foreach (var repo in projectRepos)
        {
            _log.LogInformation($"  Repo: {repo}");
        }
        return projectRepos;
    }

    private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo}".ReplaceInvalidCharactersWithDash();

    private string MigrateGithubRepoScript(string githubSourceOrg, string githubTargetOrg, string repo, string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool wait, bool skipReleases, bool lockSourceRepo, bool keepArchive)
    {
        var ghesRepoOptions = ghesApiUrl.HasValue() ? GetGhesRepoOptions(ghesApiUrl, awsBucketName, awsRegion, noSslVerify, keepArchive) : null;

        return $"gh gei migrate-repo --github-source-org \"{githubSourceOrg}\" --source-repo \"{repo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{repo}\"{(!string.IsNullOrEmpty(ghesRepoOptions) ? $" {ghesRepoOptions}" : string.Empty)}{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}{(skipReleases ? " --skip-releases" : string.Empty)}{(lockSourceRepo ? " --lock-source-repo" : string.Empty)}";
    }

    private string MigrateAdoRepoScript(string adoServerUrl, string adoSourceOrg, string teamProject, string adoRepo, string githubTargetOrg, string githubRepo, bool wait)
    {
        return $"gh gei migrate-repo{(adoServerUrl.HasValue() ? $" --ado-server-url \"{adoServerUrl}\"" : string.Empty)} --ado-source-org \"{adoSourceOrg}\" --ado-team-project \"{teamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{githubTargetOrg}\" --target-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? " --wait" : string.Empty)}";
    }

    private string GetGhesRepoOptions(string ghesApiUrl, string awsBucketName, string awsRegion, bool noSslVerify, bool keepArchive)
    {
        return $"--ghes-api-url \"{ghesApiUrl}\"{(awsBucketName.HasValue() ? $" --aws-bucket-name \"{awsBucketName}\"" : "")}{(awsRegion.HasValue() ? $" --aws-region \"{awsRegion}\"" : "")}{(noSslVerify ? " --no-ssl-verify" : string.Empty)}{(keepArchive ? " --keep-archive" : string.Empty)}";
    }

    private string WaitForMigrationScript(string repoMigrationKey = null) => $"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

    private string DownloadMigrationLogScript(string githubTargetOrg, string targetRepo)
    {
        return $"gh gei download-logs --github-target-org \"{githubTargetOrg}\" --target-repo \"{targetRepo}\"";
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
