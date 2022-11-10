using System;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Handlers;

public class MigrateCodeScanningAlertsCommandHandler : ICommandHandler<MigrateCodeScanningAlertsCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly CodeScanningAlertService _codeScanningAlertService;

    public MigrateCodeScanningAlertsCommandHandler(OctoLogger log, CodeScanningAlertService codeScanningAlertService)
    {
        _log = log;
        _codeScanningAlertService = codeScanningAlertService;
    }

    public async Task Handle(MigrateCodeScanningAlertsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        LogAndValidateOptions(args);

        await _codeScanningAlertService.MigrateCodeScanningAlerts(
            args.SourceOrg,
            args.SourceRepo,
            args.TargetOrg,
            args.TargetRepo,
            args.DryRun);

        _log.LogSuccess($"Secret scanning alerts successfully migrated.");
    }


    private void LogAndValidateOptions(MigrateCodeScanningAlertsCommandArgs args)
    {
        _log.LogInformation("Migrating Repo Code Scanning Alerts...");
        _log.LogInformation($"GITHUB SOURCE ORG: {args.SourceOrg}");
        _log.LogInformation($"SOURCE REPO: {args.SourceRepo}");

        _log.LogInformation($"GITHUB TARGET ORG: {args.TargetOrg}");
        if (args.SourceRepo.HasValue() && args.TargetRepo.IsNullOrWhiteSpace())
        {
            args.TargetRepo = args.SourceRepo;
            _log.LogInformation("Since target-repo is not provided, source-repo value will be used for target-repo.");
        }
        _log.LogInformation($"TARGET REPO: {args.TargetRepo}");

        if (args.TargetApiUrl.HasValue())
        {
            _log.LogInformation($"TARGET API URL: {args.TargetApiUrl}");
        }

        if (args.GithubSourcePat.HasValue())
        {
            _log.LogInformation("GITHUB SOURCE PAT: ***");
        }

        if (args.GithubTargetPat.HasValue())
        {
            _log.LogInformation("GITHUB TARGET PAT: ***");
        }

        if (args.GhesApiUrl.HasValue())
        {
            _log.LogInformation($"GHES API URL: {args.GhesApiUrl}");
        }

        if (args.NoSslVerify)
        {
            _log.LogInformation("SSL verification disabled");
        }

        if (args.DryRun)
        {
            _log.LogInformation("Executing in Dry Run mode, no changes will be made.");
        }
    }
}
