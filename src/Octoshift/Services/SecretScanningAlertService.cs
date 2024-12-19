using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;

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

    public virtual async Task MigrateSecretScanningAlerts(string sourceOrg, string sourceRepo, string targetOrg,
        string targetRepo, bool dryRun)
    {
        _log.LogInformation(
            $"Migrating Secret Scanning Alerts from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'");

        var sourceAlerts = await GetAlertsWithLocations(_sourceGithubApi, sourceOrg, sourceRepo);
        var targetAlerts = await GetAlertsWithLocations(_targetGithubApi, targetOrg, targetRepo);

        _log.LogInformation($"Source {sourceOrg}/{sourceRepo} secret alerts found: {sourceAlerts.Count}");
        _log.LogInformation($"Target {targetOrg}/{targetRepo} secret alerts found: {targetAlerts.Count}");

        _log.LogInformation("Matching secret resolutions from source to target repository");
        foreach (var alert in sourceAlerts)
        {
            _log.LogInformation($"Processing source secret {alert.Alert.Number}");

            if (SecretScanningAlert.IsOpen(alert.Alert.State))
            {
                _log.LogInformation("  secret alert is still open, nothing to do");
                continue;
            }

            _log.LogInformation("  secret is resolved, looking for matching secret in target...");
            var target = MatchTargetSecret(alert, targetAlerts);

            if (target == null)
            {
                _log.LogWarning(
                    $"  failed to locate a matching secret to source secret {alert.Alert.Number} in {targetOrg}/{targetRepo}");
                continue;
            }

            _log.LogInformation(
                $"  source secret alert matched alert to {target.Alert.Number} in {targetOrg}/{targetRepo}.");

            if (alert.Alert.Resolution == target.Alert.Resolution && alert.Alert.State == target.Alert.State)
            {
                _log.LogInformation("  source and target alerts are already aligned.");
                continue;
            }

            if (dryRun)
            {
                _log.LogInformation(
                    $"  executing in dry run mode! Target alert {target.Alert.Number} would have been updated to state:{alert.Alert.State} and resolution:{alert.Alert.Resolution}");
                continue;
            }

            _log.LogInformation(
                $"  updating target alert:{target.Alert.Number} to state:{alert.Alert.State} and resolution:{alert.Alert.Resolution}");

            await _targetGithubApi.UpdateSecretScanningAlert(targetOrg, targetRepo, target.Alert.Number,
            alert.Alert.State, alert.Alert.Resolution);
            _log.LogSuccess(
                $"  target alert successfully updated to {alert.Alert.Resolution}.");
        }
    }

    private AlertWithLocations MatchTargetSecret(AlertWithLocations source, List<AlertWithLocations> targets)
    {
        AlertWithLocations matched = null;

        foreach (var target in targets)
        {
            if (source.Alert.SecretType == target.Alert.SecretType
                && source.Alert.Secret == target.Alert.Secret)
            {
                _log.LogVerbose(
                    $"Secret type and value match between source:{source.Alert.Number} and target:{target.Alert.Number}");

                if (AreSecretAlertLocationsMatching(source.Locations, target.Locations))
                {
                    matched = target;
                    break;
                }
            }
        }

        return matched;
    }

    /// <summary>
    /// Determine whether or not the locations for a source and target secret scanning alerts match
    /// </summary>
    /// <param name="sourceLocations">List of locations from a source secret scanning alert</param>
    /// <param name="targetLocations">List of locations from a target secret scanning alert</param>
    /// <returns>Boolean indicating if locations match</returns>
    private bool AreSecretAlertLocationsMatching(GithubSecretScanningAlertLocation[] sourceLocations,
        GithubSecretScanningAlertLocation[] targetLocations)
    {
        var locationMatch = true;
        // Right after a code migration, as not all content gets migrated, the number of locations 
        // in the source alert will always be greater or equal to the number of locations in the 
        // target alert, hence looping through the target alert locations.
        foreach (var targetLocation in targetLocations)
        {
            locationMatch = sourceLocations.Any(
                sourceLocation => sourceLocation.Path == targetLocation.Path
                   && sourceLocation.StartLine == targetLocation.StartLine
                   && sourceLocation.EndLine == targetLocation.EndLine
                   && sourceLocation.StartColumn == targetLocation.StartColumn
                   && sourceLocation.EndColumn == targetLocation.EndColumn
                   && sourceLocation.BlobSha == targetLocation.BlobSha
                   // Technically this will hold, but only if there is not commit rewriting going on, so we need to make this last one optional for now
                   // && sourceLocation.CommitSha == targetLocation.CommitSha)       
                   );
            if (!locationMatch)
            {
                break;
            }
        }

        return locationMatch;
    }

    private async Task<List<AlertWithLocations>> GetAlertsWithLocations(GithubApi api, string org, string repo)
    {
        var alerts = await api.GetSecretScanningAlertsForRepository(org, repo);
        var results = new List<AlertWithLocations>();
        foreach (var alert in alerts)
        {
            var locations =
                await api.GetSecretScanningAlertsLocations(org, repo, alert.Number);
            results.Add(new AlertWithLocations { Alert = alert, Locations = locations.ToArray() });
        }

        return results;
    }
}

internal class AlertWithLocations
{
    public GithubSecretScanningAlert Alert { get; set; }

    public GithubSecretScanningAlertLocation[] Locations { get; set; }
}
