using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI;
using OctoshiftCLI.Models;

namespace Octoshift
{
    public class CodeScanningAlertService
    {
        private readonly GithubApi _sourceGithubApi;
        private readonly GithubApi _targetGithubApi;
        private readonly OctoLogger _log;

        public CodeScanningAlertService(GithubApi sourceGithubApi, GithubApi targetGithubApi, OctoLogger octoLogger)
        {
            _sourceGithubApi = sourceGithubApi;
            _targetGithubApi = targetGithubApi;
            _log = octoLogger;
        }

        public virtual async Task MigrateCodeScanningAlerts(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo, bool dryRun)
        {
            var defaultBranch = await _sourceGithubApi.GetDefaultBranch(sourceOrg, sourceRepo);
            _log.LogInformation($"Found default branch: {defaultBranch} - migrating code scanning alerts only of this branch.");
            var analysesSuccess = await MigrateAnalyses(sourceOrg, sourceRepo, targetOrg, targetRepo, defaultBranch, dryRun);
            if (!analysesSuccess)
            {
                _log.LogError("Aborting migration due to previous error. Please try again.");
                throw new OctoshiftCliException("Migration of Code Scanning Alerts failed.");
            }

            var alertsSuccess = await MigrateAlerts(sourceOrg, sourceRepo, targetOrg, targetRepo, defaultBranch, dryRun);
            if (!alertsSuccess)
            {
                throw new OctoshiftCliException("Migration of Code Scanning Alerts failed.");
            }
        }

        protected internal virtual async Task<bool> MigrateAnalyses(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo, string branch, bool dryRun)
        {
            _log.LogInformation($"Migrating Code Scanning Analyses from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'");

            var sourceAnalysesTask = _sourceGithubApi.GetCodeScanningAnalysisForRepository(sourceOrg, sourceRepo, branch);
            var targetAnalysesTask = _targetGithubApi.GetCodeScanningAnalysisForRepository(targetOrg, targetRepo, branch);

            await Task.WhenAll(new List<Task>
                {
                    sourceAnalysesTask,
                    targetAnalysesTask
                }
            );

            var sourceAnalyses = sourceAnalysesTask.Result.ToList();
            var targetAnalyses = targetAnalysesTask.Result.ToList();

            var relevantAnalyses = sourceAnalyses.Skip(targetAnalyses.Count).ToList();

            if (targetAnalyses.Count > 0)
            {
                _log.LogInformation(
                    $"Already found {targetAnalyses.Count} analyses on target - so {targetAnalyses.Count} of {sourceAnalyses.Count} source analyses will be skipped.");
            }


            _log.LogVerbose($"Found {relevantAnalyses.Count} analyses to migrate.");

            if (dryRun)
            {
                _log.LogInformation($"Running in dry-run mode. The following Sarif-Reports would now be downloaded from '{sourceOrg}/{sourceRepo}' and then uploaded to '{targetOrg}/{targetRepo}':");
                foreach (var analysis in relevantAnalyses)
                {
                    _log.LogInformation($"    Report of Analysis with Id '{analysis.Id}' created at {analysis.CreatedAt}.");
                }
                return true;
            }

            foreach (var analysis in relevantAnalyses)
            {
                var sarifReport = await _sourceGithubApi.GetSarifReport(sourceOrg, sourceRepo, analysis.Id);
                _log.LogVerbose($"Downloaded SARIF report for analysis {analysis.Id}");
                try
                {
                    _log.LogInformation($"Uploading SARIF for analysis {analysis.Id} in target repository...");
                    var id = await _targetGithubApi.UploadSarifReport(targetOrg, targetRepo, sarifReport, analysis.CommitSha, analysis.Ref);
                    // Wait for SARIF processing to finish before first querying it
                    await Task.Delay(500);
                    var status = await _targetGithubApi.GetSarifProcessingStatus(targetOrg, targetRepo, id);

                    while (SarifProcessingStatus.IsPending(status.Status))

                    {
                        _log.LogInformation("   SARIF processing is still pending. Waiting 5 seconds...");
                        await Task.Delay(5000);
                        status = await _targetGithubApi.GetSarifProcessingStatus(targetOrg, targetRepo, id);
                    }

                    if (SarifProcessingStatus.IsFailed(status.Status))
                    {
                        _log.LogError($"SARIF processing failed for analysis {analysis.Id}. Received the following Error(s): \n{string.Join("\n- ", status.Errors)}");
                        return false;
                    }

                    _log.LogInformation($"    Successfully migrated report for analysis {analysis.Id}");
                }
                catch (HttpRequestException httpException)
                {
                    if (httpException.StatusCode.Equals(HttpStatusCode.NotFound))
                    {
                        _log.LogError($"Received HTTP Status 404 for uploading analysis {analysis.Id}. This is either due to the target token lacking permissions to upload analysis to or generally access the target repo, or the commit with the commit-sha '{analysis.CommitSha}' is missing on the target repo.");
                    }
                    else if (httpException.StatusCode.Equals(HttpStatusCode.Forbidden))
                    {
                        _log.LogError($"Received HTTP Status 403 for uploading analysis {analysis.Id}. Please make sure to activate GitHub Advanced Security on the target.");
                    }
                    else
                    {
                        _log.LogError($"HTTP Error {httpException.StatusCode} while migrating analysis {analysis.Id}: {httpException.Message}");
                    }
                    return false;
                }
            }

            _log.LogInformation($"Successfully finished migrating {relevantAnalyses.Count} Code Scanning analyses! ");
            return true;
        }

        protected internal virtual async Task<bool> MigrateAlerts(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo, string branch, bool dryRun)
        {
            var sourceAlertTask = _sourceGithubApi.GetCodeScanningAlertsForRepository(sourceOrg, sourceRepo, branch);

            // no reason to call the target on a dry run - there will be no alerts 
            var targetAlertTask = dryRun ?
                Task.FromResult(Enumerable.Empty<CodeScanningAlert>()) :
                _targetGithubApi.GetCodeScanningAlertsForRepository(targetOrg, targetRepo, branch);

            await Task.WhenAll(new List<Task>
                {
                    sourceAlertTask,
                    targetAlertTask
                }
            );

            var sourceAlerts = sourceAlertTask.Result.ToList();
            var targetAlerts = targetAlertTask.Result.ToList();
            var successCount = 0;
            var skippedCount = 0;
            var notFoundCount = 0;

            _log.LogInformation($"Found {sourceAlerts.Count} source and {targetAlerts.Count} target alerts. Starting migration of alert states...");

            foreach (var sourceAlert in sourceAlerts)
            {
                if (!CodeScanningAlertState.IsOpenOrDismissed(sourceAlert.State))
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

                var matchingTargetAlert = await FindMatchingTargetAlert(sourceOrg, sourceRepo, targetAlerts, sourceAlert);

                if (matchingTargetAlert == null)
                {
                    _log.LogWarning($"  could not find a target alert for {sourceAlert.Number} ({sourceAlert.Url}).");
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
                await _targetGithubApi.UpdateCodeScanningAlert(
                    targetOrg,
                    targetRepo,
                    matchingTargetAlert.Number,
                    sourceAlert.State,
                    sourceAlert.DismissedReason,
                    sourceAlert.DismissedComment
                    );
                successCount++;
            }

            _log.LogInformation($"Code Scanning Alerts done!\nStatus of {sourceAlerts.Count} Alerts:\n  Success: {successCount}\n  Skipped (status not migratable or already matches): {skippedCount}\n  No matching target found (see logs): {notFoundCount}.");

            return notFoundCount == 0;
        }

        private async Task<CodeScanningAlert> FindMatchingTargetAlert(string sourceOrg, string sourceRepo, List<CodeScanningAlert> targetAlerts,
            CodeScanningAlert sourceAlert)
        {
            var targetAlertsOfSameRule = targetAlerts.Where(targetAlert => targetAlert.RuleId == sourceAlert.RuleId);
            var matchingTargetAlert = targetAlertsOfSameRule.FirstOrDefault(targetAlert => AreInstancesEqual(sourceAlert.MostRecentInstance, targetAlert.MostRecentInstance));

            if (matchingTargetAlert != null)
            {
                return matchingTargetAlert;
            }

            // Most Recent Instance is not equal, so we have to match the target alert by all instances of the source
            var allSourceInstances = await _sourceGithubApi.GetCodeScanningAlertInstances(sourceOrg, sourceRepo, sourceAlert.Number);

            return targetAlertsOfSameRule.FirstOrDefault(targetAlert => allSourceInstances.Any(sourceInstance => AreInstancesEqual(sourceInstance, targetAlert.MostRecentInstance)));
        }

        private bool AreInstancesEqual(CodeScanningAlertInstance sourceInstance,
            CodeScanningAlertInstance targetInstance)
        {
            return sourceInstance.Ref == targetInstance.Ref
                   && sourceInstance.CommitSha == targetInstance.CommitSha
                   && sourceInstance.Path == targetInstance.Path
                   && sourceInstance.StartLine == targetInstance.StartLine
                   && sourceInstance.StartColumn == targetInstance.StartColumn
                   && sourceInstance.EndLine == targetInstance.EndLine
                   && sourceInstance.EndColumn == targetInstance.EndColumn;

        }
    }
}
