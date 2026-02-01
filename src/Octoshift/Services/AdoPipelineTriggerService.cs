using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

/// <summary>
/// Service responsible for managing Azure DevOps pipeline trigger configuration during repository rewiring.
/// This service handles the complex business logic for preserving and configuring pipeline triggers
/// when moving pipelines from ADO repositories to GitHub repositories.
/// </summary>
public class AdoPipelineTriggerService
{
    private readonly AdoApi _adoApi;
    private readonly OctoLogger _log;
    private readonly string _adoBaseUrl;

    // Cache for repository IDs and branch policies to avoid redundant API calls
    private readonly Dictionary<string, AdoRepository> _repositoryCache = [];
    private readonly Dictionary<string, AdoBranchPolicyResponse> _branchPolicyCache = [];

    public AdoPipelineTriggerService(AdoApi adoApi, OctoLogger log, string adoBaseUrl)
    {
        _adoApi = adoApi ?? throw new ArgumentNullException(nameof(adoApi));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _adoBaseUrl = adoBaseUrl?.TrimEnd('/');
    }

    /// <summary>
    /// Changes a pipeline's repository configuration from ADO to GitHub, applying
    /// trigger configuration based on branch policy requirements and existing settings.
    /// </summary>
    /// <returns>True if the pipeline was successfully rewired, false if it was skipped.</returns>
    public virtual async Task<bool> RewirePipelineToGitHub(
        string adoOrg,
        string teamProject,
        int pipelineId,
        string defaultBranch,
        string clean,
        string checkoutSubmodules,
        string githubOrg,
        string githubRepo,
        string connectedServiceId,
        JToken originalTriggers = null,
        string targetApiUrl = null)
    {
        ArgumentNullException.ThrowIfNull(adoOrg);
        ArgumentNullException.ThrowIfNull(teamProject);

        var url = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

        try
        {
            var response = await _adoApi.GetAsync(url);
            var data = JObject.Parse(response);

            var currentRepoName = data["repository"]?["name"]?.ToString();
            var currentRepoId = data["repository"]?["id"]?.ToString();

            var newRepo = CreateGitHubRepositoryConfiguration(githubOrg, githubRepo, defaultBranch, clean, checkoutSubmodules, connectedServiceId, targetApiUrl);
            var isPipelineRequiredByBranchPolicy = await IsPipelineRequiredByBranchPolicy(adoOrg, teamProject, currentRepoName, currentRepoId, pipelineId);

            LogBranchPolicyCheckResults(pipelineId, isPipelineRequiredByBranchPolicy);

            var payload = BuildPipelinePayload(data, newRepo, originalTriggers, isPipelineRequiredByBranchPolicy);

            await _adoApi.PutAsync(url, payload.ToObject(typeof(object)));
            return true;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            // Pipeline not found - log warning and skip
            _log.LogWarning($"Pipeline {pipelineId} not found in {adoOrg}/{teamProject}. Skipping pipeline rewiring.");
            return false;
        }
        catch (HttpRequestException ex)
        {
            // Other HTTP errors during pipeline retrieval
            _log.LogWarning($"HTTP error retrieving pipeline {pipelineId} in {adoOrg}/{teamProject}: {ex.Message}. Skipping pipeline rewiring.");
            return false;
        }
    }

    /// <summary>
    /// Analyzes branch policies to determine if a pipeline is required for branch protection.
    /// </summary>
    public async Task<bool> IsPipelineRequiredByBranchPolicy(string adoOrg, string teamProject, string repoName, string repoId, int pipelineId)
    {
        ArgumentNullException.ThrowIfNull(adoOrg);
        ArgumentNullException.ThrowIfNull(teamProject);

        if (string.IsNullOrEmpty(repoName) && string.IsNullOrEmpty(repoId))
        {
            _log.LogWarning($"Branch policy check skipped for pipeline {pipelineId} - repository name and ID not available. Pipeline trigger configuration may not preserve branch policy requirements.");
            return false;
        }

        try
        {
            var repoInfo = await GetRepositoryIdAndStatus(adoOrg, teamProject, repoName, repoId, pipelineId);

            if (string.IsNullOrEmpty(repoInfo.Id))
            {
                return false;
            }

            if (repoInfo.IsDisabled)
            {
                var repoIdentifier = repoName ?? repoId;
                _log.LogInformation($"Repository {adoOrg}/{teamProject}/{repoIdentifier} is disabled. Branch policy check skipped for pipeline {pipelineId} - will use default trigger configuration.");
                return false;
            }

            return await CheckBranchPoliciesForPipeline(adoOrg, teamProject, repoInfo.Id, repoName, repoId, pipelineId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or ArgumentException or InvalidOperationException)
        {
            LogBranchPolicyCheckError(ex, adoOrg, teamProject, repoName, repoId, pipelineId);
            return false;
        }
    }

    private async Task<AdoRepository> GetRepositoryIdAndStatus(string adoOrg, string teamProject, string repoName, string repoId, int pipelineId)
    {
        if (!string.IsNullOrEmpty(repoId))
        {
            _log.LogVerbose($"Using repository ID from pipeline definition for branch policy check: {repoId}");
            var repoInfoById = await GetRepositoryInfoWithCache(adoOrg, teamProject, repoId, repoName);
            return new AdoRepository { Id = repoId, Name = repoName, IsDisabled = repoInfoById.IsDisabled };
        }

        var repoInfoByName = await GetRepositoryInfoWithCache(adoOrg, teamProject, null, repoName);
        if (string.IsNullOrEmpty(repoInfoByName.Id))
        {
            _log.LogWarning($"Repository ID not found for {adoOrg}/{teamProject}/{repoName}. Branch policy check cannot be performed for pipeline {pipelineId}.");
            return new AdoRepository { Id = null, Name = repoName, IsDisabled = false };
        }

        return repoInfoByName;
    }

    private async Task<bool> CheckBranchPoliciesForPipeline(string adoOrg, string teamProject, string repositoryId, string repoName, string repoId, int pipelineId)
    {
        var policyData = await GetBranchPoliciesWithCache(adoOrg, teamProject, repositoryId);

        if (policyData?.Value == null || policyData.Value.Count == 0)
        {
            var repoIdentifier = repoName ?? repoId ?? "unknown";
            _log.LogVerbose($"No branch policies found for repository {adoOrg}/{teamProject}/{repoIdentifier}. ADO Pipeline ID = {pipelineId} is not required by branch policy.");
            return false;
        }

        var isPipelineRequired = policyData.Value.Any(policy =>
            policy.Type?.DisplayName == "Build" &&
            policy.IsEnabled &&
            policy.Settings?.BuildDefinitionId == pipelineId.ToString());

        LogBranchPolicyCheckResult(isPipelineRequired, adoOrg, teamProject, repoName, repoId, pipelineId);
        return isPipelineRequired;
    }

    private void LogBranchPolicyCheckResult(bool isPipelineRequired, string adoOrg, string teamProject, string repoName, string repoId, int pipelineId)
    {
        var repoIdentifier = repoName ?? repoId ?? "unknown";

        if (isPipelineRequired)
        {
            _log.LogVerbose($"ADO Pipeline ID = {pipelineId} is required by branch policy in {adoOrg}/{teamProject}/{repoIdentifier}. Build status reporting will be enabled to support branch protection.");
        }
        else
        {
            _log.LogVerbose($"ADO Pipeline ID = {pipelineId} is not required by any branch policies in {adoOrg}/{teamProject}/{repoIdentifier}.");
        }
    }

    private void LogBranchPolicyCheckError(Exception ex, string adoOrg, string teamProject, string repoName, string repoId, int pipelineId)
    {
        var repoIdentifier = repoName ?? repoId ?? "unknown";
        var errorType = ex switch
        {
            HttpRequestException => "HTTP error",
            TaskCanceledException => "Branch policy check timed out",
            JsonException => "JSON parsing error",
            ArgumentException => "Invalid argument error",
            InvalidOperationException => "Invalid operation error",
            _ => "Error"
        };

        _log.LogWarning($"{errorType} during branch policy check for pipeline {pipelineId} in {adoOrg}/{teamProject}/{repoIdentifier}: {ex.Message}. Pipeline trigger configuration may not preserve branch policy requirements.");
    }

    #region Private Helper Methods - Trigger Configuration Logic

    private void LogBranchPolicyCheckResults(int pipelineId, bool isPipelineRequiredByBranchPolicy)
    {
        var branchPolicyMessage = isPipelineRequiredByBranchPolicy
            ? $"ADO Pipeline ID = {pipelineId} IS required by branch policy - enabling build status reporting to support branch protection"
            : $"ADO Pipeline ID = {pipelineId} is NOT required by branch policy - preserving original trigger configuration";

        _log.LogInformation(branchPolicyMessage);
    }

    private JObject BuildPipelinePayload(JObject data, object newRepo, JToken originalTriggers, bool isPipelineRequiredByBranchPolicy)
    {
        var payload = new JObject();

        foreach (var prop in data.Properties())
        {
            if (prop.Name == "repository")
            {
                prop.Value = JObject.Parse(newRepo.ToJson());
            }
            else if (prop.Name == "triggers")
            {
                prop.Value = DetermineTriggerConfiguration(originalTriggers, isPipelineRequiredByBranchPolicy);
            }

            payload.Add(prop.Name, prop.Value);
        }

        // Add triggers if no triggers property exists
        payload["triggers"] ??= DetermineTriggerConfiguration(originalTriggers, isPipelineRequiredByBranchPolicy);

        // Use YAML definitions instead of UI override settings
        // settingsSourceType: 2 = Use YAML definitions, 1 = Override from UI
        payload["settingsSourceType"] = 2;

        return payload;
    }

    private object CreateGitHubRepositoryConfiguration(string githubOrg, string githubRepo, string defaultBranch, string clean, string checkoutSubmodules, string connectedServiceId, string targetApiUrl)
    {
        var (apiUrl, _, cloneUrl, branchesUrl, refsUrl, manageUrl) = BuildGitHubUrls(githubOrg, githubRepo, targetApiUrl);

        return new
        {
            properties = new
            {
                apiUrl,
                branchesUrl,
                cloneUrl,
                connectedServiceId,
                defaultBranch,
                fullName = $"{githubOrg}/{githubRepo}",
                manageUrl,
                orgName = githubOrg,
                refsUrl,
                safeRepository = $"{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}",
                shortName = githubRepo,
                reportBuildStatus = "true" // Enable build status reporting for GitHub sources
            },
            id = $"{githubOrg}/{githubRepo}",
            type = "GitHub",
            name = $"{githubOrg}/{githubRepo}",
            url = cloneUrl,
            defaultBranch,
            clean,
            checkoutSubmodules
        };
    }

    private JToken DetermineTriggerConfiguration(JToken originalTriggers, bool isPipelineRequiredByBranchPolicy)
    {
        return isPipelineRequiredByBranchPolicy
            ? CreateBranchPolicyRequiredTriggers(originalTriggers)
            : CreateStandardTriggers(originalTriggers);
    }

    private JToken CreateBranchPolicyRequiredTriggers(JToken originalTriggers)
    {
        // Scenario 1: Pipeline IS required by branch policy
        // Enable both PR and CI triggers with build status reporting (required for branch policy integration)
        var originalCiReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "continuousIntegration");
        var originalPrReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "pullRequest");
        // For branch policy scenarios, enable reportBuildStatus if it was originally enabled OR if no original setting exists
        var enableCiBuildStatus = originalCiReportBuildStatus || originalTriggers == null || !HasTriggerType(originalTriggers, "continuousIntegration");
        var enablePrBuildStatus = originalPrReportBuildStatus || originalTriggers == null || !HasTriggerType(originalTriggers, "pullRequest");
        return CreateYamlControlledTriggers(enablePullRequestValidation: true, enableCiBuildStatusReporting: enableCiBuildStatus, enablePrBuildStatusReporting: enablePrBuildStatus);
    }

    private JToken CreateStandardTriggers(JToken originalTriggers)
    {
        // When pipeline is NOT required by branch policy, preserve existing trigger configuration
        if (originalTriggers != null)
        {
            var hadPullRequestTrigger = HasPullRequestTrigger(originalTriggers);
            var originalCiReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "continuousIntegration");
            var originalPrReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "pullRequest");
            return CreateYamlControlledTriggers(enablePullRequestValidation: hadPullRequestTrigger, enableCiBuildStatusReporting: originalCiReportBuildStatus, enablePrBuildStatusReporting: originalPrReportBuildStatus);
        }

        // Default case: Enable PR validation with build status reporting for backwards compatibility
        return CreateYamlControlledTriggers(enablePullRequestValidation: true, enableCiBuildStatusReporting: true, enablePrBuildStatusReporting: true);
    }

    private JArray CreateYamlControlledTriggers(bool enablePullRequestValidation = false, bool enableCiBuildStatusReporting = false, bool enablePrBuildStatusReporting = false)
    {
        // Create triggers that are enabled but configured to use YAML definitions
        // This enables the CI and PR validation features while letting YAML control the details
        var ciTrigger = new JObject
        {
            ["triggerType"] = "continuousIntegration",
            ["settingsSourceType"] = 2, // Use YAML definitions
            ["branchFilters"] = new JArray(), // Empty means defer to YAML
            ["pathFilters"] = new JArray(), // Empty means defer to YAML
            ["batchChanges"] = false
        };

        // Add build status reporting based on original ADO setting
        if (enableCiBuildStatusReporting)
        {
            ciTrigger["reportBuildStatus"] = "true";
        }

        var triggers = new JArray { ciTrigger };

        // Add PR trigger if requested
        if (enablePullRequestValidation)
        {
            var prTrigger = new JObject
            {
                ["triggerType"] = "pullRequest",
                ["settingsSourceType"] = 2, // Use YAML definitions
                ["isCommentRequiredForPullRequest"] = false,
                ["requireCommentsForNonTeamMembersOnly"] = false,
                ["forks"] = new JObject
                {
                    ["enabled"] = false, // Let YAML control
                    ["allowSecrets"] = false
                },
                ["branchFilters"] = new JArray(), // Empty means defer to YAML
                ["pathFilters"] = new JArray() // Empty means defer to YAML
            };

            // Add build status reporting based on original ADO setting
            if (enablePrBuildStatusReporting)
            {
                prTrigger["reportBuildStatus"] = "true";
            }

            triggers.Add(prTrigger);
        }

        return triggers;
    }

    // Helper methods for trigger analysis and manipulation

    private bool HasPullRequestTrigger(JToken originalTriggers)
    {
        if (originalTriggers == null)
        {
            return false;
        }

        // Check if any trigger has triggerType = "pullRequest"
        return originalTriggers is JArray triggerArray && triggerArray.Any(trigger =>
            trigger is JObject triggerObj &&
            triggerObj["triggerType"]?.ToString() == "pullRequest");
    }

    private bool GetOriginalReportBuildStatus(JToken originalTriggers, string triggerType)
    {
        if (originalTriggers is not JArray triggerArray)
        {
            return true; // Default to true when no original triggers exist
        }

        // Look for the specified trigger type and extract its reportBuildStatus setting
        var matchingTrigger = triggerArray
            .Where(trigger => trigger is JObject triggerObj &&
                             triggerObj["triggerType"]?.ToString() == triggerType)
            .Cast<JObject>()
            .FirstOrDefault();

        if (matchingTrigger != null)
        {
            // Return the original reportBuildStatus value, defaulting to true if not present
            var reportBuildStatusToken = matchingTrigger["reportBuildStatus"];
            if (reportBuildStatusToken == null)
            {
                return true; // Default to true when property doesn't exist
            }

            // Handle different JSON token types directly to boolean
            return reportBuildStatusToken.Type switch
            {
                JTokenType.Boolean => reportBuildStatusToken.Value<bool>(),
                JTokenType.String => string.Equals(reportBuildStatusToken.ToString(), "true", StringComparison.OrdinalIgnoreCase),
                JTokenType.None => throw new NotImplementedException(),
                JTokenType.Object => throw new NotImplementedException(),
                JTokenType.Array => throw new NotImplementedException(),
                JTokenType.Constructor => throw new NotImplementedException(),
                JTokenType.Property => throw new NotImplementedException(),
                JTokenType.Comment => throw new NotImplementedException(),
                JTokenType.Integer => throw new NotImplementedException(),
                JTokenType.Float => throw new NotImplementedException(),
                JTokenType.Null => throw new NotImplementedException(),
                JTokenType.Undefined => throw new NotImplementedException(),
                JTokenType.Date => throw new NotImplementedException(),
                JTokenType.Raw => throw new NotImplementedException(),
                JTokenType.Bytes => throw new NotImplementedException(),
                JTokenType.Guid => throw new NotImplementedException(),
                JTokenType.Uri => throw new NotImplementedException(),
                JTokenType.TimeSpan => throw new NotImplementedException(),
                _ => TryConvertToBool(reportBuildStatusToken)
            };
        }

        return true; // Default to true when trigger type not found in original triggers
    }

    private static bool TryConvertToBool(JToken token)
    {
        try
        {
            return token.ToObject<bool>();
        }
        catch (JsonException)
        {
            return true; // Default to true if JSON conversion fails
        }
        catch (InvalidOperationException)
        {
            return true; // Default to true if operation is invalid
        }
        catch (ArgumentException)
        {
            return true; // Default to true if argument is invalid
        }
        catch (FormatException)
        {
            return true; // Default to true if format is invalid
        }
    }

    private bool HasTriggerType(JToken originalTriggers, string triggerType)
    {
        if (originalTriggers is not JArray triggerArray)
        {
            return false;
        }

        // Check if the specified trigger type exists
        return triggerArray
            .OfType<JObject>()
            .Where(triggerObj => triggerObj["triggerType"]?.ToString() == triggerType)
            .Any();
    }

    private (string apiUrl, string webUrl, string cloneUrl, string branchesUrl, string refsUrl, string manageUrl) BuildGitHubUrls(string githubOrg, string githubRepo, string targetApiUrl)
    {
        if (targetApiUrl.HasValue())
        {
            var apiUri = new Uri(targetApiUrl.TrimEnd('/'));
            var webHost = apiUri.Host.StartsWith("api.") ? apiUri.Host[4..] : apiUri.Host;
            var webScheme = apiUri.Scheme;
            var webBase = $"{webScheme}://{webHost}";
            var apiUrl = $"{targetApiUrl.TrimEnd('/')}/repos/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}";
            var webUrl = $"{webBase}/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}";
            var cloneUrl = $"{webBase}/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}.git";
            var branchesUrl = $"{targetApiUrl.TrimEnd('/')}/repos/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}/branches";
            var refsUrl = $"{targetApiUrl.TrimEnd('/')}/repos/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}/git/refs";
            return (apiUrl, webUrl, cloneUrl, branchesUrl, refsUrl, webUrl);
        }
        else
        {
            var apiUrl = $"https://api.github.com/repos/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}";
            var webUrl = $"https://github.com/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}";
            var cloneUrl = $"https://github.com/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}.git";
            var branchesUrl = $"https://api.github.com/repos/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}/branches";
            var refsUrl = $"https://api.github.com/repos/{githubOrg.EscapeDataString()}/{githubRepo.EscapeDataString()}/git/refs";
            return (apiUrl, webUrl, cloneUrl, branchesUrl, refsUrl, webUrl);
        }
    }

    #region Private Helper Methods - Caching

    /// <summary>
    /// Gets the repository information (ID and disabled status) with caching to avoid redundant API calls for the same repository.
    /// </summary>
    private async Task<AdoRepository> GetRepositoryInfoWithCache(string adoOrg, string teamProject, string repoId, string repoName)
    {
        var identifier = !string.IsNullOrEmpty(repoId) ? repoId : repoName;
        var cacheKey = $"{adoOrg.ToUpper()}/{teamProject.ToUpper()}/{identifier.ToUpper()}";

        if (_repositoryCache.TryGetValue(cacheKey, out var cachedInfo))
        {
            _log.LogVerbose($"Using cached repository information for {adoOrg}/{teamProject}/{identifier}");
            return cachedInfo;
        }

        _log.LogVerbose($"Fetching repository information for {adoOrg}/{teamProject}/{identifier}");

        try
        {
            var repoUrl = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{identifier.EscapeDataString()}?api-version=6.0";
            var repoResponse = await _adoApi.GetAsync(repoUrl);
            var repoData = JObject.Parse(repoResponse);
            var repositoryId = repoData["id"]?.ToString();
            var isDisabled = repoData["isDisabled"]?.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            if (!string.IsNullOrEmpty(repositoryId))
            {
                var repoInfo = new AdoRepository { Id = repositoryId, Name = identifier, IsDisabled = isDisabled };
                _repositoryCache[cacheKey] = repoInfo;
                _log.LogVerbose($"Cached repository information (ID: {repositoryId}, Disabled: {isDisabled}) for {adoOrg}/{teamProject}/{identifier}");
                return repoInfo;
            }

            return new AdoRepository { Id = null, Name = identifier, IsDisabled = false };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            // 404 typically means the repository is disabled or doesn't exist
            // Treat it as disabled to avoid further API calls
            // Log as verbose since the caller will log a more specific warning about the disabled repository
            // Return (null, true) to indicate repository ID is unknown but repository is disabled
            _log.LogVerbose($"Repository {adoOrg}/{teamProject}/{identifier} returned 404 - likely disabled or not found.");
            var repoInfo = new AdoRepository { Id = null, Name = identifier, IsDisabled = true }; // Mark as disabled with null ID since identifier may be a name
            _repositoryCache[cacheKey] = repoInfo;
            return repoInfo;
        }
        catch (HttpRequestException ex)
        {
            // Don't cache failed requests - let the caller handle the error
            _log.LogVerbose($"Failed to fetch repository information for {adoOrg}/{teamProject}/{identifier}: {ex.Message}");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Don't cache timeouts - let the caller handle the error
            _log.LogVerbose($"Timeout fetching repository information for {adoOrg}/{teamProject}/{identifier}: {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            // Don't cache JSON parsing errors - let the caller handle the error
            _log.LogVerbose($"JSON parsing error for repository {adoOrg}/{teamProject}/{identifier}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the branch policies with caching to avoid redundant API calls for the same repository.
    /// </summary>
    private async Task<AdoBranchPolicyResponse> GetBranchPoliciesWithCache(string adoOrg, string teamProject, string repositoryId)
    {
        var cacheKey = $"{adoOrg.ToUpper()}/{teamProject.ToUpper()}/{repositoryId.ToUpper()}";

        if (_branchPolicyCache.TryGetValue(cacheKey, out var cachedPolicies))
        {
            _log.LogVerbose($"Using cached branch policies for repository ID {repositoryId}");
            return cachedPolicies;
        }

        _log.LogVerbose($"Fetching branch policies for repository ID {repositoryId}");

        try
        {
            var policyUrl = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";
            var policyResponse = await _adoApi.GetAsync(policyUrl);
            var policyData = JsonConvert.DeserializeObject<AdoBranchPolicyResponse>(policyResponse);

            if (policyData != null)
            {
                _branchPolicyCache[cacheKey] = policyData;
                _log.LogVerbose($"Cached {policyData.Value?.Count ?? 0} branch policies for repository ID {repositoryId}");
            }

            return policyData;
        }
        catch (HttpRequestException ex)
        {
            // Don't cache failed requests - let the caller handle the error
            _log.LogVerbose($"Failed to fetch branch policies for repository ID {repositoryId}: {ex.Message}");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Don't cache timeouts - let the caller handle the error
            _log.LogVerbose($"Timeout fetching branch policies for repository ID {repositoryId}: {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            // Don't cache JSON parsing errors - let the caller handle the error
            _log.LogVerbose($"JSON parsing error for branch policies repository ID {repositoryId}: {ex.Message}");
            throw;
        }
    }

    #endregion

    #endregion
}
