using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.GenerateScript;

public class GenerateScriptCommandHandler : ICommandHandler<GenerateScriptCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
    private GenerateScriptOptions _generateScriptOptions;
    private readonly IVersionProvider _versionProvider;
    private readonly AdoInspectorService _adoInspectorService;

    public GenerateScriptCommandHandler(OctoLogger log, AdoApi adoApi, IVersionProvider versionProvider, AdoInspectorService adoInspectorService)
    {
        _log = log;
        _adoApi = adoApi;
        _versionProvider = versionProvider;
        _adoInspectorService = adoInspectorService;
    }

    public async Task Handle(GenerateScriptCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Generating Script...");

        _generateScriptOptions = new GenerateScriptOptions
        {
            CreateTeams = args.All || args.CreateTeams || args.LinkIdpGroups,
            LinkIdpGroups = args.All || args.LinkIdpGroups,
            LockAdoRepos = args.All || args.LockAdoRepos,
            DisableAdoRepos = args.All || args.DisableAdoRepos,
            RewirePipelines = args.All || args.RewirePipelines,
            DownloadMigrationLogs = args.All || args.DownloadMigrationLogs
        };

        _adoInspectorService.OrgFilter = args.AdoOrg;
        _adoInspectorService.TeamProjectFilter = args.AdoTeamProject;

        if (args.RepoList.HasValue())
        {
            _log.LogInformation($"Loading Repo CSV File...");
            _adoInspectorService.LoadReposCsv(args.RepoList.FullName);
        }

        if (await _adoInspectorService.GetRepoCount() == 0)
        {
            _log.LogError("A migration script could not be generated because no migratable repos were found. Please note that the GEI does not migrate disabled or TFVC repos.");
            return;
        }

        var appIds = _generateScriptOptions.RewirePipelines ? await GetAppIds(_adoApi, args.GithubOrg) : new Dictionary<string, string>();

        var script = args.Sequential
            ? await GenerateSequentialScript(appIds, args.GithubOrg, args.AdoServerUrl, args.TargetApiUrl)
            : await GenerateParallelScript(appIds, args.GithubOrg, args.AdoServerUrl, args.TargetApiUrl);

        _adoInspectorService.OutputRepoListToLog();

        await CheckForDuplicateRepoNames();

        if (args.Output.HasValue())
        {
            await WriteToFile(args.Output.FullName, script);
        }
    }

    private async Task<IDictionary<string, string>> GetAppIds(AdoApi ado, string githubOrg)
    {
        var appIds = new Dictionary<string, string>();

        foreach (var org in await _adoInspectorService.GetOrgs())
        {
            // Not using AdoInspector here because we want all team projects here, even if we are filtering to a specific one
            var appId = await ado.GetGithubAppId(org, githubOrg, await ado.GetTeamProjects(org));

            if (appId.HasValue())
            {
                appIds.Add(org, appId);
            }
            else
            {
                _log.LogWarning($"CANNOT FIND GITHUB APP SERVICE CONNECTION IN ADO ORGANIZATION: {org}. You must install the Pipelines app in GitHub and connect it to any Team Project in this ADO Org first.");
            }
        }

        return appIds;
    }

    private async Task CheckForDuplicateRepoNames()
    {
        var seen = new HashSet<string>();

        foreach (var org in await _adoInspectorService.GetOrgs())
        {
            foreach (var teamProject in await _adoInspectorService.GetTeamProjects(org))
            {
                foreach (var repo in (await _adoInspectorService.GetRepos(org, teamProject))
                    .Where(repo => !seen.Add(GetGithubRepoName(teamProject, repo.Name))))
                {
                    _log.LogWarning($"DUPLICATE REPO NAME: {GetGithubRepoName(teamProject, repo.Name)}");
                }
            }
        }
    }

    private string GetGithubRepoName(string adoTeamProject, string repo) => $"{adoTeamProject}-{repo}".ReplaceInvalidCharactersWithDash();

    private string GetRepoMigrationKey(string adoOrg, string githubRepoName) => $"{adoOrg}/{githubRepoName}";

    private async Task<string> GenerateSequentialScript(IDictionary<string, string> appIds, string githubOrg, string adoServerUrl, string targetApiUrl)
    {
        var content = new StringBuilder();

        AppendLine(content, PWSH_SHEBANG);
        AppendLine(content);
        AppendLine(content, VersionComment);
        AppendLine(content, EXEC_FUNCTION_BLOCK);
        AppendLine(content, VALIDATE_ENV_VARS);

        foreach (var adoOrg in await _adoInspectorService.GetOrgs())
        {
            AppendLine(content, $"# =========== Organization: {adoOrg} ===========");

            appIds.TryGetValue(adoOrg, out var appId);

            if (_generateScriptOptions.RewirePipelines && appId is null)
            {
                AppendLine(content, "# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
            }

            foreach (var adoTeamProject in await _adoInspectorService.GetTeamProjects(adoOrg))
            {
                AppendLine(content);
                AppendLine(content, $"# === Team Project: {adoOrg}/{adoTeamProject} ===");

                if (!(await _adoInspectorService.GetRepos(adoOrg, adoTeamProject)).Any())
                {
                    AppendLine(content, "# Skipping this Team Project because it has no git repos");
                    continue;
                }

                AppendLine(content, Exec(CreateGithubMaintainersTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups, targetApiUrl)));
                AppendLine(content, Exec(CreateGithubAdminsTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups, targetApiUrl)));
                AppendLine(content, Exec(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId)));

                foreach (var adoRepo in await _adoInspectorService.GetRepos(adoOrg, adoTeamProject))
                {
                    var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo.Name);

                    AppendLine(content);
                    AppendLine(content, Exec(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                    AppendLine(content, Exec(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo.Name, githubOrg, githubRepo, true, adoServerUrl, targetApiUrl)));
                    AppendLine(content, Exec(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                    AppendLine(content, Exec(AddMaintainersToGithubRepoScript(adoTeamProject, githubOrg, githubRepo, targetApiUrl)));
                    AppendLine(content, Exec(AddAdminsToGithubRepoScript(adoTeamProject, githubOrg, githubRepo, targetApiUrl)));
                    AppendLine(content, Exec(DownloadMigrationLogScript(githubOrg, githubRepo, targetApiUrl)));

                    foreach (var adoPipeline in await _adoInspectorService.GetPipelines(adoOrg, adoTeamProject, adoRepo.Name))
                    {
                        AppendLine(content, Exec(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId)));
                    }
                }
            }

            AppendLine(content);
            AppendLine(content);
        }

        return content.ToString();
    }

    private async Task<string> GenerateParallelScript(IDictionary<string, string> appIds, string githubOrg, string adoServerUrl, string targetApiUrl)
    {
        var content = new StringBuilder();
        AppendLine(content, PWSH_SHEBANG);
        AppendLine(content);
        AppendLine(content, VersionComment);
        AppendLine(content, EXEC_FUNCTION_BLOCK);
        AppendLine(content, EXEC_AND_GET_MIGRATION_ID_FUNCTION_BLOCK);
        AppendLine(content, EXEC_BATCH_FUNCTION_BLOCK);
        AppendLine(content, VALIDATE_ENV_VARS);

        AppendLine(content);
        AppendLine(content, "$Succeeded = 0");
        AppendLine(content, "$Failed = 0");
        AppendLine(content, "$RepoMigrations = [ordered]@{}");

        // Queueing migrations
        foreach (var adoOrg in await _adoInspectorService.GetOrgs())
        {
            AppendLine(content);
            AppendLine(content, $"# =========== Queueing migration for Organization: {adoOrg} ===========");

            appIds.TryGetValue(adoOrg, out var appId);

            if (_generateScriptOptions.RewirePipelines && appId is null)
            {
                AppendLine(content);
                AppendLine(content, "# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
            }

            foreach (var adoTeamProject in await _adoInspectorService.GetTeamProjects(adoOrg))
            {
                AppendLine(content);
                AppendLine(content, $"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");

                if (!(await _adoInspectorService.GetRepos(adoOrg, adoTeamProject)).Any())
                {
                    AppendLine(content, "# Skipping this Team Project because it has no git repos");
                    continue;
                }

                AppendLine(content, Exec(CreateGithubMaintainersTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups, targetApiUrl)));
                AppendLine(content, Exec(CreateGithubAdminsTeamScript(adoTeamProject, githubOrg, _generateScriptOptions.LinkIdpGroups, targetApiUrl)));
                AppendLine(content, Exec(ShareServiceConnectionScript(adoOrg, adoTeamProject, appId)));

                // queue up repo migration for each ADO repo
                foreach (var adoRepo in await _adoInspectorService.GetRepos(adoOrg, adoTeamProject))
                {

                    var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo.Name);

                    AppendLine(content);
                    AppendLine(content, Exec(LockAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                    AppendLine(content, QueueMigrateRepoScript(adoOrg, adoTeamProject, adoRepo.Name, githubOrg, githubRepo, adoServerUrl, targetApiUrl));
                    AppendLine(content, $"$RepoMigrations[\"{GetRepoMigrationKey(adoOrg, githubRepo)}\"] = $MigrationID");
                }
            }
        }

        // Waiting for migrations
        foreach (var adoOrg in await _adoInspectorService.GetOrgs())
        {
            AppendLine(content);
            AppendLine(content, $"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");

            foreach (var adoTeamProject in await _adoInspectorService.GetTeamProjects(adoOrg))
            {
                foreach (var adoRepo in await _adoInspectorService.GetRepos(adoOrg, adoTeamProject))
                {
                    AppendLine(content);
                    AppendLine(content, $"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {adoRepo.Name}. Will then complete the below post migration steps. ===");

                    var githubRepo = GetGithubRepoName(adoTeamProject, adoRepo.Name);
                    var repoMigrationKey = GetRepoMigrationKey(adoOrg, githubRepo);

                    AppendLine(content, "$CanExecuteBatch = $false");
                    AppendLine(content, $"if ($null -ne $RepoMigrations[\"{repoMigrationKey}\"]) {{");
                    AppendLine(content, "    " + WaitForMigrationScript(repoMigrationKey, targetApiUrl));
                    AppendLine(content, "    $CanExecuteBatch = ($lastexitcode -eq 0)");

                    AppendLine(content, "}");
                    AppendLine(content, "if ($CanExecuteBatch) {");
                    if (
                        _generateScriptOptions.CreateTeams ||
                        _generateScriptOptions.DisableAdoRepos ||
                        _generateScriptOptions.RewirePipelines ||
                        _generateScriptOptions.DownloadMigrationLogs
                    )
                    {
                        AppendLine(content, "    ExecBatch @(");
                        AppendLine(content, "        " + Wrap(DisableAdoRepoScript(adoOrg, adoTeamProject, adoRepo.Name)));
                        AppendLine(content, "        " + Wrap(AddMaintainersToGithubRepoScript(adoTeamProject, githubOrg, githubRepo, targetApiUrl)));
                        AppendLine(content, "        " + Wrap(AddAdminsToGithubRepoScript(adoTeamProject, githubOrg, githubRepo, targetApiUrl)));
                        AppendLine(content, "        " + Wrap(DownloadMigrationLogScript(githubOrg, githubRepo, targetApiUrl)));

                        appIds.TryGetValue(adoOrg, out var appId);
                        foreach (var adoPipeline in await _adoInspectorService.GetPipelines(adoOrg, adoTeamProject, adoRepo.Name))
                        {
                            AppendLine(content, "        " + Wrap(RewireAzurePipelineScript(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, appId)));
                        }

                        AppendLine(content, "    )");
                        AppendLine(content, "    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
                    }
                    else
                    {
                        AppendLine(content, "    $Succeeded++");
                    }

                    AppendLine(content, "} else {");
                    AppendLine(content, "    $Failed++");
                    AppendLine(content, "}");
                }
            }
        }

        // Generating report
        AppendLine(content);
        AppendLine(content, "Write-Host =============== Summary ===============");
        AppendLine(content, "Write-Host Total number of successful migrations: $Succeeded");
        AppendLine(content, "Write-Host Total number of failed migrations: $Failed");

        AppendLine(content, @"
if ($Failed -ne 0) {
    exit 1
}");

        AppendLine(content);
        AppendLine(content);

        return content.ToString();
    }

    private void AppendLine(StringBuilder sb, string content)
    {
        if (content.IsNullOrWhiteSpace())
        {
            return;
        }

        sb.AppendLine(content);
    }

    private void AppendLine(StringBuilder sb) => sb.AppendLine();

    private string DisableAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo) =>
        _generateScriptOptions.DisableAdoRepos
            ? $"gh ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
            : null;

    private string LockAdoRepoScript(string adoOrg, string adoTeamProject, string adoRepo) =>
        _generateScriptOptions.LockAdoRepos
            ? $"gh ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
            : null;

    private string ShareServiceConnectionScript(string adoOrg, string adoTeamProject, string appId) =>
        _generateScriptOptions.RewirePipelines && appId.HasValue()
            ? $"gh ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
            : null;

    private string MigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, bool wait, string adoServerUrl, string targetApiUrl) =>
        $"gh ado2gh migrate-repo{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\"{(_log.Verbose ? " --verbose" : string.Empty)}{(wait ? string.Empty : " --queue-only")} --target-repo-visibility private{(adoServerUrl.HasValue() ? $" --ado-server-url \"{adoServerUrl}\"" : string.Empty)}";

    private string QueueMigrateRepoScript(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, string adoServerUrl, string targetApiUrl) =>
        $"$MigrationID = {ExecAndGetMigrationId(MigrateRepoScript(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, false, adoServerUrl, targetApiUrl))}";

    private string CreateGithubMaintainersTeamScript(string adoTeamProject, string githubOrg, bool linkIdpGroups, string targetApiUrl) =>
        _generateScriptOptions.CreateTeams
            ? $"gh ado2gh create-team{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --github-org \"{githubOrg}\" --team-name \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Maintainers\"{(_log.Verbose ? " --verbose" : string.Empty)}{(linkIdpGroups ? $" --idp-group \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Maintainers\"" : string.Empty)}"
            : null;

    private string CreateGithubAdminsTeamScript(string adoTeamProject, string githubOrg, bool linkIdpGroups, string targetApiUrl) =>
        _generateScriptOptions.CreateTeams
            ? $"gh ado2gh create-team{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --github-org \"{githubOrg}\" --team-name \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Admins\"{(_log.Verbose ? " --verbose" : string.Empty)}{(linkIdpGroups ? $" --idp-group \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Admins\"" : string.Empty)}"
            : null;

    private string AddMaintainersToGithubRepoScript(string adoTeamProject, string githubOrg, string githubRepo, string targetApiUrl) =>
        _generateScriptOptions.CreateTeams
            ? $"gh ado2gh add-team-to-repo{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Maintainers\" --role \"maintain\"{(_log.Verbose ? " --verbose" : string.Empty)}"
            : null;

    private string AddAdminsToGithubRepoScript(string adoTeamProject, string githubOrg, string githubRepo, string targetApiUrl) =>
        _generateScriptOptions.CreateTeams
            ? $"gh ado2gh add-team-to-repo{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --team \"{adoTeamProject.ReplaceInvalidCharactersWithDash()}-Admins\" --role \"admin\"{(_log.Verbose ? " --verbose" : string.Empty)}"
            : null;

    private string RewireAzurePipelineScript(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string appId) =>
        _generateScriptOptions.RewirePipelines && appId.HasValue()
            ? $"gh ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{adoPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\" --service-connection-id \"{appId}\"{(_log.Verbose ? " --verbose" : string.Empty)}"
            : null;

    private string WaitForMigrationScript(string repoMigrationKey, string targetApiUrl) => $"gh ado2gh wait-for-migration{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --migration-id $RepoMigrations[\"{repoMigrationKey}\"]";

    private string DownloadMigrationLogScript(string githubOrg, string githubRepo, string targetApiUrl) =>
        _generateScriptOptions.DownloadMigrationLogs
        ? $"gh ado2gh download-logs{(targetApiUrl.HasValue() ? $" --target-api-url \"{targetApiUrl}\"" : string.Empty)} --github-org \"{githubOrg}\" --github-repo \"{githubRepo}\""
        : null;

    private string Exec(string script) => Wrap(script, "Exec");


    private string ExecAndGetMigrationId(string script) => Wrap(script, "ExecAndGetMigrationID");

    private string Wrap(string script, string outerCommand = "") =>
        script.IsNullOrWhiteSpace() ? string.Empty : $"{outerCommand} {{ {script} }}".Trim();


    private class GenerateScriptOptions
    {
        public bool CreateTeams { get; init; }
        public bool LinkIdpGroups { get; init; }
        public bool LockAdoRepos { get; init; }
        public bool DisableAdoRepos { get; init; }
        public bool RewirePipelines { get; init; }
        public bool DownloadMigrationLogs { get; init; }
    }

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

    private const string EXEC_BATCH_FUNCTION_BLOCK = @"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}";
    private const string VALIDATE_ENV_VARS = @"
if (-not $env:ADO_PAT) {
    Write-Error ""ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#personal-access-tokens-for-azure-devops""
    exit 1
} else {
    Write-Host ""ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps.""
}

if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}";
}
