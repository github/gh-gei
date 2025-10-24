using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandHandler : ICommandHandler<MigrateRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly WarningsCountLogger _warningsCountLogger;

    public MigrateRepoCommandHandler(OctoLogger log, GithubApi githubApi, EnvironmentVariableProvider environmentVariableProvider, WarningsCountLogger warningsCountLogger)
    {
        _log = log;
        _githubApi = githubApi;
        _environmentVariableProvider = environmentVariableProvider;
        _warningsCountLogger = warningsCountLogger;
    }

    public async Task Handle(MigrateRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Repo...");

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();

        var adoRepoUrl = GetAdoRepoUrl(args.AdoOrg, args.AdoTeamProject, args.AdoRepo, args.AdoServerUrl);

        args.AdoPat ??= _environmentVariableProvider.AdoPersonalAccessToken();
        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);

        string migrationSourceId;

        try
        {
            migrationSourceId = await _githubApi.CreateAdoMigrationSource(githubOrgId, args.AdoServerUrl);
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
        {
            var insufficientPermissionsMessage = InsufficientPermissionsMessageGenerator.Generate(args.GithubOrg);
            var message = $"{ex.Message}{insufficientPermissionsMessage}";
            throw new OctoshiftCliException(message, ex);
        }

        string migrationId;

        try
        {
            migrationId = await _githubApi.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, args.GithubRepo, args.AdoPat, args.GithubPat, targetRepoVisibility: args.TargetRepoVisibility);
        }
        catch (OctoshiftCliException ex)
        {
            if (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
            {
                _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
                return;
            }

            throw;
        }

        if (args.QueueOnly)
        {
            _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
            return;
        }

        var (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);

        while (RepositoryMigrationStatus.IsPending(migrationState))
        {
            _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 60 seconds...");
            await Task.Delay(60000);
            (migrationState, _, warningsCount, failureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);
        }

        var migrationLogAvailableMessage = $"Migration log available at {migrationLogUrl} or by running `gh {CliContext.RootCommand} download-logs --github-org {args.GithubOrg} --github-repo {args.GithubRepo}`";

        if (RepositoryMigrationStatus.IsFailed(migrationState))
        {
            _log.LogError($"Migration Failed. Migration ID: {migrationId}");
            _warningsCountLogger.LogWarningsCount(warningsCount);
            _log.LogInformation(migrationLogAvailableMessage);
            throw new OctoshiftCliException(failureReason);
        }

        _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        _warningsCountLogger.LogWarningsCount(warningsCount);
        _log.LogInformation(migrationLogAvailableMessage);

        // Clean status checks if requested
        if (args.CleanStatusChecks)
        {
            await CleanStatusChecksFromBranchProtection(args.GithubOrg, args.GithubRepo);
        }
    }

    private string GetAdoRepoUrl(string org, string project, string repo, string serverUrl)
    {
        serverUrl = serverUrl.HasValue() ? serverUrl.TrimEnd('/') : "https://dev.azure.com";
        return $"{serverUrl}/{org.EscapeDataString()}/{project.EscapeDataString()}/_git/{repo.EscapeDataString()}";
    }

    private async Task CleanStatusChecksFromBranchProtection(string org, string repo)
    {
        try
        {
            _log.LogInformation("Cleaning status checks from default branch protection...");

            // Get only the default branch to optimize performance
            var defaultBranch = await _githubApi.GetDefaultBranch(org, repo);

            if (string.IsNullOrEmpty(defaultBranch))
            {
                _log.LogInformation("No default branch found, skipping status check cleanup");
                return;
            }

            _log.LogInformation($"Processing default branch: {defaultBranch}");

            try
            {
                var protection = await _githubApi.GetBranchProtection(org, repo, defaultBranch);

                if (protection != null)
                {
                    await CleanStatusChecksFromProtection(org, repo, defaultBranch, protection);
                }
                else
                {
                    _log.LogInformation($"No branch protection found for default branch '{defaultBranch}', skipping");
                }
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning($"Failed to process branch protection for default branch '{defaultBranch}': {ex.Message}");
            }
            catch (OctoshiftCliException ex)
            {
                _log.LogWarning($"Failed to process branch protection for default branch '{defaultBranch}': {ex.Message}");
            }

            _log.LogSuccess("Successfully cleaned status checks from default branch protection");
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning($"Failed to clean status checks: {ex.Message}");
        }
        catch (OctoshiftCliException ex)
        {
            _log.LogWarning($"Failed to clean status checks: {ex.Message}");
        }
    }

    private async Task CleanStatusChecksFromProtection(string org, string repo, string branch, JObject protection)
    {
        // Check if required_status_checks exists and clean the check list while keeping the requirement enabled
        if (protection["required_status_checks"] != null && protection["required_status_checks"].Type != JTokenType.Null)
        {
            _log.LogInformation($"Cleaning status checks for branch '{branch}'");

            // Keep "Require status checks to pass before merging" enabled but clean the status check list
            // GitHub API requires contexts array to be present (can be empty)
            var statusChecksSettings = new
            {
                strict = ExtractBooleanValue(protection["required_status_checks"]?["strict"]) || true, // Default to true if not set
                contexts = Array.Empty<string>() // Empty array as per GitHub API documentation examples
            };

            // Create a proper update payload that conforms to GitHub API schema
            var updatePayload = new
            {
                required_status_checks = statusChecksSettings, // Keep enabled but with no specific checks
                enforce_admins = ExtractBooleanValue(protection["enforce_admins"]),
                required_pull_request_reviews = ExtractPullRequestReviewsSettings(protection["required_pull_request_reviews"]),
                restrictions = ExtractRestrictionsSettings(protection["restrictions"]),
                required_linear_history = ExtractBooleanValue(protection["required_linear_history"]),
                allow_force_pushes = ExtractBooleanValue(protection["allow_force_pushes"]),
                allow_deletions = ExtractBooleanValue(protection["allow_deletions"]),
                block_creations = ExtractBooleanValue(protection["block_creations"]),
                required_conversation_resolution = ExtractBooleanValue(protection["required_conversation_resolution"]),
                lock_branch = ExtractBooleanValue(protection["lock_branch"]),
                allow_fork_syncing = ExtractBooleanValue(protection["allow_fork_syncing"])
            };

            await _githubApi.UpdateBranchProtection(org, repo, branch, updatePayload);
        }
        else
        {
            _log.LogInformation($"No status checks found for branch '{branch}', skipping");
        }
    }

    private static bool ExtractBooleanValue(JToken token) =>
        token switch
        {
            null => false,
            { Type: JTokenType.Null } => false,
            { Type: JTokenType.Boolean } => (bool)token,
            { Type: JTokenType.Object } => token["enabled"]?.Value<bool>() ?? false,
            _ => false
        };

    private static object ExtractPullRequestReviewsSettings(JToken token) =>
        token?.Type == JTokenType.Null ? null : token?.DeepClone();

    private static object ExtractRestrictionsSettings(JToken token) =>
        token?.Type == JTokenType.Null ? null : token?.DeepClone();
}
