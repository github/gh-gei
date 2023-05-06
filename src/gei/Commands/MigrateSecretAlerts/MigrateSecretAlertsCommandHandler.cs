using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateSecretAlerts;

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
        _log.LogInformation("Migrating Secret Scanning Alerts...");

        if (args.SourceRepo.HasValue() && args.TargetRepo.IsNullOrWhiteSpace())
        {
            args.TargetRepo = args.SourceRepo;
            _log.LogInformation("Since target-repo is not provided, source-repo value will be used for target-repo.");
        }
    }
}
