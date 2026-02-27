using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateDependabotAlerts;

public class MigrateDependabotAlertsCommandHandler : ICommandHandler<MigrateDependabotAlertsCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly DependabotAlertService _dependabotAlertService;

    public MigrateDependabotAlertsCommandHandler(OctoLogger log, DependabotAlertService dependabotAlertService)
    {
        _log = log;
        _dependabotAlertService = dependabotAlertService;
    }

    public async Task Handle(MigrateDependabotAlertsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Migrating Repo Dependabot Alerts...");

        await _dependabotAlertService.MigrateDependabotAlerts(
            args.SourceOrg,
            args.SourceRepo,
            args.TargetOrg,
            args.TargetRepo,
            args.DryRun);

        if (!args.DryRun)
        {
            _log.LogSuccess($"Dependabot alerts successfully migrated.");
        }
    }
}
