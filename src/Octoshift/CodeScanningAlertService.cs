using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI;

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
            await MigrateAnalyses(sourceOrg, sourceRepo, targetOrg, targetRepo, defaultBranch, dryRun);
            await MigrateAlerts(sourceOrg, sourceRepo, targetOrg, targetRepo, defaultBranch, dryRun);
        }

        protected internal virtual async Task MigrateAnalyses(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo, string branch, bool dryRun)
        {
            _log.LogInformation($"Migrating Code Scanning Analyses from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'");

            var analyses = await _sourceGithubApi.GetCodeScanningAnalysisForRepository(sourceOrg, sourceRepo, branch);
            analyses = analyses.ToList();

            var successCount = 0;
            var errorCount = 0;

            _log.LogVerbose($"Found {analyses.Count()} analyses to migrate.");

            if (dryRun)
            {
                _log.LogInformation($"Running in dry-run mode. The following Sarif-Reports would now be downloaded from '{sourceOrg}/{sourceRepo}' and then uploaded to '{targetOrg}/{targetRepo}':");
                foreach (var analysis in analyses)
                {
                    _log.LogInformation($"    Report of Analysis with Id '{analysis.Id}' created at {analysis.CreatedAt}.");
                }
                return;
            }

            foreach (var analysis in analyses)
            {
                var sarifReport = await _sourceGithubApi.GetSarifReport(sourceOrg, sourceRepo, analysis.Id);
                _log.LogVerbose($"Downloaded SARIF report for analysis {analysis.Id}");
                try
                {
                    await _targetGithubApi.UploadSarifReport(targetOrg, targetRepo,
                        new SarifContainer
                        {
                            Sarif = sarifReport,
                            Ref = analysis.Ref,
                            CommitSha = analysis.CommitSha
                        });
                    _log.LogInformation($"Successfully Migrated report for analysis {analysis.Id}");
                    ++successCount;
                }
                catch (HttpRequestException httpException)
                {
                    if (httpException.StatusCode.Equals(HttpStatusCode.NotFound))
                    {
                        _log.LogVerbose($"No commit found on target. Skipping Analysis {analysis.Id}");
                    }
                    else
                    {
                        _log.LogWarning($"Http Error {httpException.StatusCode} while migrating analysis {analysis.Id}: ${httpException.Message}");
                    }
                    ++errorCount;
                }
                catch (Exception exception)
                {
                    _log.LogWarning($"Fatal Error while uploading SARIF report for analysis {analysis.Id}: \n {exception.Message}");
                    _log.LogError(exception);
                    // Todo Maybe throw another exception here?
                    throw;
                }
                _log.LogInformation($"Handled {successCount + errorCount} / {analyses.Count()} Analyses.");
            }

            _log.LogInformation($"Code Scanning Analyses done!\nSuccess-Count: {successCount}\nError-Count: {errorCount}\nOverall: {analyses.Count()}.");
        }

        protected internal virtual async Task MigrateAlerts(string sourceOrg, string sourceRepo, string targetOrg,
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

            _log.LogInformation($"Found {sourceAlerts.Count} source and {targetAlerts.Count} target alerts. Starting migration of alert states...");

            foreach (var sourceAlert in sourceAlerts)
            {
                if (!CodeScanningAlerts.IsOpenOrDismissed(sourceAlert.State))
                {
                    _log.LogInformation($"  skipping alert {sourceAlert.Number} ({sourceAlert.Url}) because state '{sourceAlert.State}' is not migratable.");
                    continue;
                }

                if (dryRun)
                {
                    _log.LogInformation($"  running in dry-run mode. Would have tried to find target alert for {sourceAlert.Number} ({sourceAlert.Url}) and set state '{sourceAlert.State}'");
                    successCount++;
                    continue;
                }


                var matchingTargetAlert = targetAlerts.Find(targetAlert => AreAlertsEqual(sourceAlert, targetAlert));

                if (matchingTargetAlert == null)
                {
                    _log.LogWarning($"Could not find target alert for {sourceAlert.Number} ({sourceAlert.Url})");
                    continue;
                }

                // Todo: Add this to a queue to parallelize alert updates
                _log.LogVerbose($"Setting Status ${sourceAlert.State} for target alert ${matchingTargetAlert.Number} (${matchingTargetAlert.Url})");
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

            _log.LogInformation($"Code Scanning Alerts done!\nSuccess-Count: {successCount}/ {sourceAlerts.Count} migrated!");

        }

        private bool AreAlertsEqual(CodeScanningAlert sourceAlert, CodeScanningAlert targetAlert)
        {
            return sourceAlert.RuleId == targetAlert.RuleId
                   && sourceAlert.Instance.Ref == targetAlert.Instance.Ref
                   && sourceAlert.Instance.CommitSha == targetAlert.Instance.CommitSha
                   && sourceAlert.Instance.Location.Path == targetAlert.Instance.Location.Path
                   && sourceAlert.Instance.Location.StartLine == targetAlert.Instance.Location.StartLine
                   && sourceAlert.Instance.Location.StartColumn == targetAlert.Instance.Location.StartColumn
                   && sourceAlert.Instance.Location.EndLine == targetAlert.Instance.Location.EndLine
                   && sourceAlert.Instance.Location.EndColumn == targetAlert.Instance.Location.EndColumn;
        }
    }

}
