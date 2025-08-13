using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public virtual async Task RewirePipelineToGitHub(
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
        var url = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

        var response = await _adoApi.GetAsync(url);
        var data = JObject.Parse(response);

        var newRepo = CreateGitHubRepositoryConfiguration(githubOrg, githubRepo, defaultBranch, clean, checkoutSubmodules, connectedServiceId, targetApiUrl);
        var currentRepoName = data["repository"]?["name"]?.ToString();
        var (isPipelineRequiredByBranchPolicy, branchPolicyCheckSucceeded) = await CheckBranchPolicyRequirement(adoOrg, teamProject, currentRepoName, pipelineId);

        LogBranchPolicyCheckResults(pipelineId, isPipelineRequiredByBranchPolicy, branchPolicyCheckSucceeded);

        var payload = BuildPipelinePayload(data, newRepo, originalTriggers, isPipelineRequiredByBranchPolicy, branchPolicyCheckSucceeded);

        await _adoApi.PutAsync(url, payload.ToObject(typeof(object)));
    }

    /// <summary>
    /// Analyzes branch policies to determine if a pipeline is required for branch protection.
    /// </summary>
    public async Task<bool> IsPipelineRequiredByBranchPolicy(string adoOrg, string teamProject, string repoName, int pipelineId)
    {
        try
        {
            // Get repository information first
            var repoUrl = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repoName.EscapeDataString()}?api-version=6.0";
            var repoResponse = await _adoApi.GetAsync(repoUrl);
            var repoData = JObject.Parse(repoResponse);
            var repositoryId = repoData["id"]?.ToString();

            if (string.IsNullOrEmpty(repositoryId))
            {
                _log.LogWarning($"Repository ID not found for {adoOrg}/{teamProject}/{repoName}. Branch policy check cannot be performed for pipeline {pipelineId}.");
                return false;
            }

            // Get branch policies for the repository
            var policyUrl = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";
            var policyResponse = await _adoApi.GetAsync(policyUrl);
            var policyData = JObject.Parse(policyResponse);

            if (policyData["value"] is not JArray policies)
            {
                _log.LogVerbose($"No branch policies found for repository {adoOrg}/{teamProject}/{repoName}. ADO Pipeline ID = {pipelineId} is not required by branch policy.");
                return false;
            }

            // Look for build validation policies that reference our pipeline
            foreach (var policy in policies)
            {
                if (policy is not JObject policyObj)
                {
                    continue;
                }

                // Check if this is a build validation policy
                var policyType = policyObj["type"]?["displayName"]?.ToString();
                if (policyType != "Build")
                {
                    continue;
                }

                // Check if the policy is enabled
                var isEnabled = policyObj["isEnabled"]?.Value<bool>() == true;
                if (!isEnabled)
                {
                    continue;
                }

                // Check if this policy references our pipeline by ID (not by display name)
                var buildDefinitionId = policyObj["settings"]?["buildDefinitionId"]?.ToString();

                // Match by pipeline ID since display names can be different from pipeline names
                if (buildDefinitionId != null && buildDefinitionId.Equals(pipelineId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogVerbose($"ADO Pipeline ID = {pipelineId} is required by branch policy in {adoOrg}/{teamProject}/{repoName}. Build status reporting will be enabled to support branch protection.");
                    return true;
                }
            }

            _log.LogVerbose($"ADO Pipeline ID = {pipelineId} is not required by any branch policies in {adoOrg}/{teamProject}/{repoName}.");
            return false;
        }
        catch (HttpRequestException ex)
        {
            // If we can't determine branch policy status due to network issues, default to false
            _log.LogWarning($"HTTP error during branch policy check for pipeline {pipelineId} in {adoOrg}/{teamProject}/{repoName}: {ex.Message}");
            return false;
        }
        catch (JsonException ex)
        {
            // If we can't determine branch policy status due to JSON parsing issues, default to false
            _log.LogWarning($"JSON parsing error during branch policy check for pipeline {pipelineId} in {adoOrg}/{teamProject}/{repoName}: {ex.Message}");
            return false;
        }
        catch (ArgumentException ex)
        {
            // If we can't determine branch policy status due to invalid arguments, default to false
            _log.LogWarning($"Invalid argument error during branch policy check for pipeline {pipelineId} in {adoOrg}/{teamProject}/{repoName}: {ex.Message}");
            return false;
        }
    }

    #region Private Helper Methods - Trigger Configuration Logic

    private async Task<(bool isRequired, bool checkSucceeded)> CheckBranchPolicyRequirement(string adoOrg, string teamProject, string currentRepoName, int pipelineId)
    {
        if (string.IsNullOrEmpty(currentRepoName))
        {
            _log.LogWarning($"Branch policy check skipped for pipeline {pipelineId} - repository name not available. Pipeline trigger configuration may not preserve branch policy requirements.");
            return (false, false);
        }

        try
        {
            var isRequired = await IsPipelineRequiredByBranchPolicy(adoOrg, teamProject, currentRepoName, pipelineId);
            return (isRequired, true);
        }
        catch (HttpRequestException ex)
        {
            // If branch policy checking fails due to network/HTTP issues, consider check failed
            _log.LogWarning($"Branch policy check failed for pipeline {pipelineId} in {adoOrg}/{teamProject}/{currentRepoName} due to network/HTTP error: {ex.Message}. Pipeline trigger configuration may not preserve branch policy requirements.");
            return (false, false);
        }
        catch (TaskCanceledException ex)
        {
            // If branch policy checking times out, consider check failed
            _log.LogWarning($"Branch policy check timed out for pipeline {pipelineId} in {adoOrg}/{teamProject}/{currentRepoName}: {ex.Message}. Pipeline trigger configuration may not preserve branch policy requirements.");
            return (false, false);
        }
        catch (JsonException ex)
        {
            // If branch policy checking fails due to JSON parsing issues, consider check failed
            _log.LogWarning($"Branch policy check failed for pipeline {pipelineId} in {adoOrg}/{teamProject}/{currentRepoName} due to JSON parsing error: {ex.Message}. Pipeline trigger configuration may not preserve branch policy requirements.");
            return (false, false);
        }
        catch (ArgumentException ex)
        {
            // If branch policy checking fails due to invalid arguments, consider check failed
            _log.LogWarning($"Branch policy check failed for pipeline {pipelineId} in {adoOrg}/{teamProject}/{currentRepoName} due to invalid arguments: {ex.Message}. Pipeline trigger configuration may not preserve branch policy requirements.");
            return (false, false);
        }
        catch (InvalidOperationException ex)
        {
            // If branch policy checking fails due to invalid state, consider check failed
            _log.LogWarning($"Branch policy check failed for pipeline {pipelineId} in {adoOrg}/{teamProject}/{currentRepoName} due to invalid operation: {ex.Message}. Pipeline trigger configuration may not preserve branch policy requirements.");
            return (false, false);
        }
    }

    private void LogBranchPolicyCheckResults(int pipelineId, bool isPipelineRequiredByBranchPolicy, bool branchPolicyCheckSucceeded)
    {
        var branchPolicyMessage = isPipelineRequiredByBranchPolicy
            ? $"ADO Pipeline ID = {pipelineId} IS required by branch policy - enabling build status reporting to support branch protection"
            : branchPolicyCheckSucceeded
                ? $"ADO Pipeline ID = {pipelineId} is NOT required by branch policy - preserving original trigger configuration"
                : $"Branch policy check FAILED for ADO Pipeline ID = {pipelineId} - using fallback trigger configuration";

        _log.LogInformation(branchPolicyMessage);
    }

    private JObject BuildPipelinePayload(JObject data, object newRepo, JToken originalTriggers, bool isPipelineRequiredByBranchPolicy, bool branchPolicyCheckSucceeded)
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
                prop.Value = DetermineTriggerConfiguration(originalTriggers, isPipelineRequiredByBranchPolicy, branchPolicyCheckSucceeded);
            }

            payload.Add(prop.Name, prop.Value);
        }

        // Add triggers if no triggers property exists
        payload["triggers"] ??= DetermineTriggerConfiguration(originalTriggers, isPipelineRequiredByBranchPolicy, branchPolicyCheckSucceeded);

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

    private JToken DetermineTriggerConfiguration(JToken originalTriggers, bool isPipelineRequiredByBranchPolicy, bool branchPolicyCheckSucceeded)
    {
        return isPipelineRequiredByBranchPolicy
            ? CreateBranchPolicyRequiredTriggers(originalTriggers)
            : branchPolicyCheckSucceeded
                ? CreateSuccessfulBranchPolicyCheckTriggers(originalTriggers)
                : CreateFailedBranchPolicyCheckTriggers(originalTriggers);
    }

    private JToken CreateBranchPolicyRequiredTriggers(JToken originalTriggers)
    {
        // Scenario 1: Pipeline IS required by branch policy
        // Enable both PR and CI triggers with build status reporting (required for branch policy integration)
        var originalCiReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "continuousIntegration");
        var originalPrReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "pullRequest");
        // For branch policy scenarios, enable reportBuildStatus if it was originally enabled OR if no original setting exists
        var enableCiBuildStatus = IsReportBuildStatusEnabled(originalCiReportBuildStatus) || originalTriggers == null || !HasTriggerType(originalTriggers, "continuousIntegration");
        var enablePrBuildStatus = IsReportBuildStatusEnabled(originalPrReportBuildStatus) || originalTriggers == null || !HasTriggerType(originalTriggers, "pullRequest");
        return CreateYamlControlledTriggers(enablePullRequestValidation: true, enableCiBuildStatusReporting: enableCiBuildStatus, enablePrBuildStatusReporting: enablePrBuildStatus);
    }

    private JToken CreateSuccessfulBranchPolicyCheckTriggers(JToken originalTriggers)
    {
        // Scenario 2a: Pipeline NOT required by branch policy, but check was successful
        // Preserve existing triggers regardless of complexity
        if (originalTriggers != null)
        {
            var hadPullRequestTrigger = HasPullRequestTrigger(originalTriggers);
            var originalCiReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "continuousIntegration");
            var originalPrReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "pullRequest");
            return CreateYamlControlledTriggers(enablePullRequestValidation: hadPullRequestTrigger, enableCiBuildStatusReporting: IsReportBuildStatusEnabled(originalCiReportBuildStatus), enablePrBuildStatusReporting: IsReportBuildStatusEnabled(originalPrReportBuildStatus));
        }

        // Default case: Enable PR validation with build status reporting for backwards compatibility
        return CreateYamlControlledTriggers(enablePullRequestValidation: true, enableCiBuildStatusReporting: true, enablePrBuildStatusReporting: true);
    }

    private JToken CreateFailedBranchPolicyCheckTriggers(JToken originalTriggers)
    {
        if (originalTriggers != null && HasCompleteTriggerSet(originalTriggers))
        {
            // Scenario 2b: Branch policy check failed/not performed, but has rich trigger configuration
            // Preserve existing rich triggers with proper UI structure
            var hadPullRequestTrigger = HasPullRequestTrigger(originalTriggers);
            var originalCiReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "continuousIntegration");
            var originalPrReportBuildStatus = GetOriginalReportBuildStatus(originalTriggers, "pullRequest");
            return CreateYamlControlledTriggers(enablePullRequestValidation: hadPullRequestTrigger, enableCiBuildStatusReporting: IsReportBuildStatusEnabled(originalCiReportBuildStatus), enablePrBuildStatusReporting: IsReportBuildStatusEnabled(originalPrReportBuildStatus));
        }
        else if (originalTriggers != null)
        {
            // Scenario 2c: Branch policy check failed/not performed, has minimal trigger configuration
            // Use YAML-only approach (empty triggers array) to let YAML completely control triggers
            return new JArray();
        }
        else
        {
            // Default case: No original triggers and branch policy check failed
            // For basic rewiring scenarios (backwards compatibility), enable both PR validation and build status reporting
            return CreateYamlControlledTriggers(enablePullRequestValidation: true, enableCiBuildStatusReporting: true, enablePrBuildStatusReporting: true);
        }
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
                    ["enabled"] = false,  - let YAML control
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

    private bool HasCompleteTriggerSet(JToken originalTriggers)
    {
        if (originalTriggers is not JArray triggerArray)
        {
            return false;
        }

        // Check if any trigger has rich configuration that should be preserved
        foreach (var trigger in triggerArray)
        {
            if (trigger is not JObject triggerObj)
            {
                continue;
            }

            // Check for rich PR trigger configuration
            if (triggerObj["triggerType"]?.ToString() == "pullRequest" &&
                (triggerObj["forks"] != null ||
                 triggerObj["isCommentRequiredForPullRequest"] != null ||
                 triggerObj["requireCommentsForNonTeamMembersOnly"] != null ||
                 triggerObj["autoCancel"] != null ||
                 triggerObj["settingsSourceType"] != null))
            {
                return true;
            }

            // Check for rich CI trigger configuration
            if (triggerObj["triggerType"]?.ToString() == "continuousIntegration" &&
                (triggerObj["batchChanges"] != null ||
                 triggerObj["settingsSourceType"] != null ||
                 triggerObj["maxConcurrentBuildsPerBranch"] != null))
            {
                return true;
            }
        }

        return false;
    }

    private string GetOriginalReportBuildStatus(JToken originalTriggers, string triggerType)
    {
        if (originalTriggers is not JArray triggerArray)
        {
            return "true"; // Default to "true" when no original triggers exist
        }

        // Look for the specified trigger type and extract its reportBuildStatus setting
        var matchingTrigger = triggerArray
            .Where(trigger => trigger is JObject triggerObj &&
                             triggerObj["triggerType"]?.ToString() == triggerType)
            .Cast<JObject>()
            .FirstOrDefault();

        if (matchingTrigger != null)
        {
            // Return the original reportBuildStatus value, defaulting to "true" if not present
            var reportBuildStatusToken = matchingTrigger["reportBuildStatus"];
            if (reportBuildStatusToken == null)
            {
                return "true"; // Default to "true" when property doesn't exist
            }

            // Handle both boolean and string values - normalize to string
            if (reportBuildStatusToken.Type == JTokenType.Boolean)
            {
                return reportBuildStatusToken.Value<bool>() ? "true" : "false";
            }
            else if (reportBuildStatusToken.Type == JTokenType.String)
            {
                var stringValue = reportBuildStatusToken.ToString();
                // Normalize to lowercase for consistency
                return string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(stringValue, "True", StringComparison.OrdinalIgnoreCase)
                    ? "true"
                    : "false";
            }

            // Try to convert any other type to boolean, then to string
            try
            {
                return reportBuildStatusToken.ToObject<bool>() ? "true" : "false";
            }
            catch (JsonException)
            {
                return "true"; // Default to "true" if JSON conversion fails
            }
            catch (InvalidOperationException)
            {
                return "true"; // Default to "true" if operation is invalid
            }
        }

        return "true"; // Default to "true" when trigger type not found in original triggers
    }

    private static bool IsReportBuildStatusEnabled(string reportBuildStatusValue)
    {
        return string.Equals(reportBuildStatusValue, "true", StringComparison.OrdinalIgnoreCase);
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

    #endregion
}
