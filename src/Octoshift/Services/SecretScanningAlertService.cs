using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class SecretScanningAlertService
{
    private readonly GithubApi _sourceGithubApi;
    private readonly GithubApi _targetGithubApi;
    private readonly OctoLogger _log;

    public SecretScanningAlertService(GithubApi sourceGithubApi, GithubApi targetGithubApi, OctoLogger logger)
    {
        _sourceGithubApi = sourceGithubApi;
        _targetGithubApi = targetGithubApi;
        _log = logger;
    }

    // Iterate over all source alerts by looping through the dictionary with each key (SecretType, Secret) and 
    // try to find a matching alert in the target repository based on the same key
    // If potential match is found we compare the locations of the alerts and if they match a matching AlertWithLocations is returned
    public virtual async Task MigrateSecretScanningAlerts(string sourceOrg, string sourceRepo, string targetOrg,
    string targetRepo, bool dryRun)
    {
        _log.LogInformation($"Migrating Secret Scanning Alerts from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'");

        var sourceAlertsDict = await GetAlertsWithLocations(_sourceGithubApi, sourceOrg, sourceRepo);
        var targetAlertsDict = await GetAlertsWithLocations(_targetGithubApi, targetOrg, targetRepo);

        _log.LogInformation($"Source {sourceOrg}/{sourceRepo} secret alerts found: {sourceAlertsDict.Count}");
        _log.LogInformation($"Target {targetOrg}/{targetRepo} secret alerts found: {targetAlertsDict.Count}");

        _log.LogInformation("Matching secret resolutions from source to target repository");

        foreach (var kvp in sourceAlertsDict)
        {
            var sourceKey = kvp.Key;
            var sourceAlerts = kvp.Value;

            foreach (var sourceAlert in sourceAlerts)
            {
                _log.LogInformation($"Processing source secret {sourceAlert.Alert.Number}");

                if (SecretScanningAlert.IsOpen(sourceAlert.Alert.State))
                {
                    _log.LogInformation("  secret alert is still open, nothing to do");
                    continue;
                }

                _log.LogInformation("  secret is resolved, looking for matching secret in target...");

                if (targetAlertsDict.TryGetValue(sourceKey, out var potentialTargets))
                {
                    var targetAlert = potentialTargets.FirstOrDefault(target => DoAllLocationsMatch(sourceAlert.Locations, target.Locations));

                    if (targetAlert != null)
                    {
                        _log.LogInformation($"  source secret alert matched to {targetAlert.Alert.Number} in {targetOrg}/{targetRepo}.");

                        if (sourceAlert.Alert.Resolution == targetAlert.Alert.Resolution && sourceAlert.Alert.State == targetAlert.Alert.State)
                        {
                            _log.LogInformation("  source and target alerts are already aligned.");
                            continue;
                        }

                        if (dryRun)
                        {
                            _log.LogInformation($"  executing in dry run mode! Target alert {targetAlert.Alert.Number} would have been updated to state:{sourceAlert.Alert.State} and resolution:{sourceAlert.Alert.Resolution}");
                            continue;
                        }

                        _log.LogInformation($"  updating target alert:{targetAlert.Alert.Number} to state:{sourceAlert.Alert.State} and resolution:{sourceAlert.Alert.Resolution}");

                        var prefix = $"[@{sourceAlert.Alert.ResolverName}] ";
                        var originalComment = sourceAlert.Alert.ResolutionComment ?? string.Empty;
                        var prefixedComment = prefix + originalComment;

                        var targetResolutionComment = prefixedComment.Length <= 270
                            ? prefixedComment
                            : prefix + originalComment[..Math.Max(0, 270 - prefix.Length)];

                        await _targetGithubApi.UpdateSecretScanningAlert(targetOrg, targetRepo, targetAlert.Alert.Number, sourceAlert.Alert.State,
                            sourceAlert.Alert.Resolution, targetResolutionComment);
                        _log.LogSuccess($"  target alert successfully updated to {sourceAlert.Alert.Resolution} with comment {targetResolutionComment}.");
                    }
                    else
                    {
                        _log.LogWarning($"  failed to locate a matching secret to source secret {sourceAlert.Alert.Number} in {targetOrg}/{targetRepo}");
                    }
                }
                else
                {
                    _log.LogWarning($"  Failed to locate a matching secret to source secret {sourceAlert.Alert.Number} in {targetOrg}/{targetRepo}");
                }
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0075: Conditional expression can be simplified", Justification = "Want to keep guard for better performance.")]
    private bool DoAllLocationsMatch(GithubSecretScanningAlertLocation[] sourceLocations, GithubSecretScanningAlertLocation[] targetLocations)
    {
        // Preflight check: Compare the number of locations; 
        // If the number of locations don't match we can skip the detailed comparison as the alerts can't be considered equal
        return sourceLocations.Length != targetLocations.Length
            ? false
            : sourceLocations.All(sourceLocation => IsLocationMatched(sourceLocation, targetLocations));
    }

    private bool IsLocationMatched(GithubSecretScanningAlertLocation sourceLocation, GithubSecretScanningAlertLocation[] targetLocations)
    {
        return targetLocations.Any(targetLocation => AreLocationsEqual(sourceLocation, targetLocation));
    }

    // Check if the locations of the source and target alerts match exactly
    // We compare the type of location and the corresponding fields based on the type
    // Each type has different fields that need to be compared for equality so we use a switch statement
    // Note: Discussions are commented out as we don't migrate them currently
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0075: Conditional expression can be simplified", Justification = "Want to keep guard for better performance.")]
    private bool AreLocationsEqual(GithubSecretScanningAlertLocation sourceLocation, GithubSecretScanningAlertLocation targetLocation)
    {
        return sourceLocation.LocationType != targetLocation.LocationType
            ? false
            : sourceLocation.LocationType switch
            {
                "commit" or "wiki_commit" => sourceLocation.Path == targetLocation.Path &&
                                            sourceLocation.StartLine == targetLocation.StartLine &&
                                            sourceLocation.EndLine == targetLocation.EndLine &&
                                            sourceLocation.StartColumn == targetLocation.StartColumn &&
                                            sourceLocation.EndColumn == targetLocation.EndColumn &&
                                            sourceLocation.BlobSha == targetLocation.BlobSha,
                // For all other location types, we match on the final path segment of the relevant URL
                // because the rest of the URL is going to be different between source and target org/repo
                _ => CompareUrlIds(GetLocationUrl(sourceLocation), GetLocationUrl(targetLocation))
            };
    }

    // Getting alerts with locations from a repository and building a dictionary with a key (SecretType, Secret)
    // and value List of AlertWithLocations
    // This method is used to get alerts from both source and target repositories
    private async Task<Dictionary<(string SecretType, string Secret), List<AlertWithLocations>>>
       GetAlertsWithLocations(GithubApi api, string org, string repo)
    {
        var alerts = await api.GetSecretScanningAlertsForRepository(org, repo);
        var alertsWithLocations = new List<AlertWithLocations>();
        foreach (var alert in alerts)
        {
            var locations = await api.GetSecretScanningAlertsLocations(org, repo, alert.Number);
            alertsWithLocations.Add(new AlertWithLocations { Alert = alert, Locations = locations.ToArray() });
        }

        // Build the dictionary keyed by SecretType and Secret
        return alertsWithLocations
            .GroupBy(alert => (alert.Alert.SecretType, alert.Alert.Secret))
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    // Compares the final segment of an URL which is relevant for the comparison
    private bool CompareUrlIds(string sourceUrl, string targetUrl)
    {
        return sourceUrl.HasValue() && targetUrl.HasValue() && sourceUrl.TrimEnd('/').Split('/').Last()
            == targetUrl.TrimEnd('/').Split('/').Last();
    }

    // Get the URL of the location based on its type
    private string GetLocationUrl(GithubSecretScanningAlertLocation location)
    {
        return location.LocationType switch
        {
            "issue_title" => location.IssueTitleUrl,
            "issue_body" => location.IssueBodyUrl,
            "issue_comment" => location.IssueCommentUrl,
            "pull_request_title" => location.PullRequestTitleUrl,
            "pull_request_body" => location.PullRequestBodyUrl,
            "pull_request_comment" => location.PullRequestCommentUrl,
            "pull_request_review" => location.PullRequestReviewUrl,
            "pull_request_review_comment" => location.PullRequestReviewCommentUrl,
            _ => null
        };
    }
}

internal class AlertWithLocations
{
    public GithubSecretScanningAlert Alert { get; set; }

    public GithubSecretScanningAlertLocation[] Locations { get; set; }
}
