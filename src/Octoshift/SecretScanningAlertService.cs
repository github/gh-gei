using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;

namespace OctoshiftCLI;

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
            if (matched != null)
            {
                break;
            }

            if (source.Alert.SecretType == target.Alert.SecretType
                && source.Alert.Secret == target.Alert.Secret)
            {
                _log.LogVerbose(
                    $"Secret type and value match between source:{source.Alert.Number} and target:{source.Alert.Number}");
                var locationMatch = true;
                foreach (var sourceLocation in source.Locations)
                {
                    locationMatch = IsMatchedSecretAlertLocation(sourceLocation, target.Locations);
                    if (!locationMatch)
                    {
                        break;
                    }
                }

                if (locationMatch)
                {
                    matched = target;
                }
            }
        }

        return matched;
    }

    private bool IsMatchedSecretAlertLocation(GithubSecretScanningAlertLocation sourceLocation,
        GithubSecretScanningAlertLocation[] targetLocations)
    {
        // We cannot guarantee the ordering of things with the locations and the APIs, typically they would match, but cannot be sure
        // so we need to iterate over all the targets to ensure a match
        return targetLocations.Any(
            target => sourceLocation.Path == target.Path
                   && sourceLocation.StartLine == target.StartLine
                   && sourceLocation.EndLine == target.EndLine
                   && sourceLocation.StartColumn == target.StartColumn
                   && sourceLocation.EndColumn == target.EndColumn
                   && sourceLocation.BlobSha == target.BlobSha
                   // Technically this wil hold, but only if there is not commit rewriting going on, so we need to make this last one optional for now
                   // && sourceDetails.CommitSha == target.Details.CommitSha)       
                   );
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
