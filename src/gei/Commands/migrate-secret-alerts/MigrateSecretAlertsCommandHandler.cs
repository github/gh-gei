using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class MigrateSecretAlertsCommandHandler : ICommandHandler<MigrateSecretAlertsCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly SecretScanningAlertService _secretScanningAlertService;

    public MigrateSecretAlertsCommandHandler(OctoLogger log, SecretScanningAlertService secretScanningAlertService)
    {
        _log = log;
        _secretScanningAlertService = secretScanningAlertService;
    }

    public async Task Handle(MigrateSecretAlertsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Secret Scanning Alerts...");

        if (args.DryRun)
        {
            _log.LogInformation("Executing in Dry Run mode, no changes will be made.");
        }

        ValidateOptions(args);

        await _secretScanningAlertService.MigrateSecretScanningAlerts(
            args.SourceOrg,
            args.SourceRepo,
            args.TargetOrg,
            args.TargetRepo,
            args.DryRun);

        _log.LogSuccess($"Secret scanning alerts successfully migrated.");
    }

    private void ValidateOptions(MigrateSecretAlertsCommandArgs args)
    {
        if (args.SourceRepo.HasValue() && args.TargetRepo.IsNullOrWhiteSpace())
        {
            args.TargetRepo = args.SourceRepo;
            _log.LogInformation("Since target-repo is not provided, source-repo value will be used for target-repo.");
        }
    }
}
