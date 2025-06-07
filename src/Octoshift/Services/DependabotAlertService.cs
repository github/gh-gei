using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI.Models;

namespace OctoshiftCLI.Services;

public class DependabotAlertService
{
    private readonly GithubApi _sourceGithubApi;
    private readonly GithubApi _targetGithubApi;
    private readonly OctoLogger _log;

    public DependabotAlertService(GithubApi sourceGithubApi, GithubApi targetGithubApi, OctoLogger octoLogger)
    {
        _sourceGithubApi = sourceGithubApi;
        _targetGithubApi = targetGithubApi;
        _log = octoLogger;
    }

    public virtual async Task MigrateDependabotAlerts(string sourceOrg, string sourceRepo, string targetOrg,
        string targetRepo, bool dryRun)
    {
        _log.LogInformation($"Migrating Dependabot Alerts from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'");

        var sourceAlerts = (await _sourceGithubApi.GetDependabotAlertsForRepository(sourceOrg, sourceRepo)).ToList();

        // no reason to call the target on a dry run - there will be no alerts 
        var targetAlerts = dryRun ?
            [] :
            (await _targetGithubApi.GetDependabotAlertsForRepository(targetOrg, targetRepo)).ToList();

        var successCount = 0;
        var skippedCount = 0;
        var notFoundCount = 0;

        _log.LogInformation($"Found {sourceAlerts.Count} source and {targetAlerts.Count} target alerts. Starting migration of alert states...");

        foreach (var sourceAlert in sourceAlerts)
        {
            if (!DependabotAlertState.IsOpenOrDismissed(sourceAlert.State))
            {
                _log.LogInformation($"  skipping alert {sourceAlert.Number} ({sourceAlert.Url}) because state '{sourceAlert.State}' is not migratable.");
                skippedCount++;
                continue;
            }

            if (dryRun)
            {
                _log.LogInformation($"  running in dry-run mode. Would have tried to find target alert for {sourceAlert.Number} ({sourceAlert.Url}) and set state '{sourceAlert.State}'");
                successCount++;
                // No sense in continuing here, because we don't have the target alert as it is not migrated in dryRun mode
                continue;
            }

            var matchingTargetAlert = FindMatchingTargetAlert(targetAlerts, sourceAlert);

            if (matchingTargetAlert == null)
            {
                _log.LogError($"  could not find a target alert for {sourceAlert.Number} ({sourceAlert.Url}).");
                notFoundCount++;
                continue;
            }

            if (matchingTargetAlert.State == sourceAlert.State)
            {
                _log.LogInformation("  skipping alert because target alert already has the same state.");
                skippedCount++;
                continue;
            }

            _log.LogVerbose($"Setting Status {sourceAlert.State} for target alert {matchingTargetAlert.Number} ({matchingTargetAlert.Url})");
            await _targetGithubApi.UpdateDependabotAlert(
                targetOrg,
                targetRepo,
                matchingTargetAlert.Number,
                sourceAlert.State,
                sourceAlert.DismissedReason,
                sourceAlert.DismissedComment
                );
            successCount++;
        }

        _log.LogInformation($"Dependabot Alerts done!\nStatus of {sourceAlerts.Count} Alerts:\n  Success: {successCount}\n  Skipped (status not migratable or already matches): {skippedCount}\n  No matching target found (see logs): {notFoundCount}.");

        if (notFoundCount > 0)
        {
            throw new OctoshiftCliException("Migration of Dependabot Alerts failed.");
        }
    }

    private DependabotAlert FindMatchingTargetAlert(List<DependabotAlert> targetAlerts, DependabotAlert sourceAlert)
    {
        // Try to match based on the security advisory GHSA ID and package name
        var matchingAlert = targetAlerts.FirstOrDefault(targetAlert =>
            targetAlert.SecurityAdvisory?.GhsaId == sourceAlert.SecurityAdvisory?.GhsaId &&
            targetAlert.Dependency?.Package == sourceAlert.Dependency?.Package &&
            targetAlert.Dependency?.Manifest == sourceAlert.Dependency?.Manifest);

        if (matchingAlert != null)
        {
            return matchingAlert;
        }

        // Fall back to matching by CVE ID if GHSA ID doesn't match
        return targetAlerts.FirstOrDefault(targetAlert =>
            targetAlert.SecurityAdvisory?.CveId == sourceAlert.SecurityAdvisory?.CveId &&
            targetAlert.Dependency?.Package == sourceAlert.Dependency?.Package &&
            targetAlert.Dependency?.Manifest == sourceAlert.Dependency?.Manifest);
    }
}