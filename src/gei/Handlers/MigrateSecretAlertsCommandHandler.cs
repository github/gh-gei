using System;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Handlers;

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

        _log.Verbose = args.Verbose;

        LogAndValidateOptions(args);

        await _secretScanningAlertService.MigrateSecretScanningAlerts(
            args.SourceOrg,
            args.SourceRepo,
            args.TargetOrg,
            args.TargetRepo,
            args.DryRun);

        _log.LogSuccess($"Secret scanning alerts successfully migrated.");
    }

    private void LogAndValidateOptions(MigrateSecretAlertsCommandArgs args)
    {
        _log.LogInformation("Migrating Secret Scanning Alerts...");

        if (string.IsNullOrWhiteSpace(args.SourceOrg))
        {
            throw new OctoshiftCliException("Missing GitHub source organization name, please set --github-source-org");
        }
        _log.LogInformation($"GITHUB SOURCE ORG: {args.SourceOrg}");
        _log.LogInformation($"SOURCE REPO: {args.SourceRepo}");

        _log.LogInformation($"GITHUB TARGET ORG: {args.TargetOrg}");
        if (args.SourceRepo.HasValue() && args.TargetRepo.IsNullOrWhiteSpace())
        {
            args.TargetRepo = args.SourceRepo;
            _log.LogInformation("Since target-repo is not provided, source-repo value will be used for target-repo vaule.");
        }
        _log.LogInformation($"TARGET REPO: {args.TargetRepo}");

        if (!string.IsNullOrWhiteSpace(args.TargetApiUrl))
        {
            _log.LogInformation($"TARGET API URL: {args.TargetApiUrl}");
        }

        if (args.GithubSourcePat is not null)
        {
            _log.LogInformation("GITHUB SOURCE PAT: ***");
        }

        if (args.GithubTargetPat is not null)
        {
            _log.LogInformation("GITHUB TARGET PAT: ***");

            if (args.GithubSourcePat is null)
            {
                args.GithubSourcePat = args.GithubTargetPat;
                _log.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
            }
        }

        if (string.IsNullOrWhiteSpace(args.TargetRepo))
        {
            _log.LogInformation(
                $"Target repo name not provided, defaulting to same as source repo ({args.SourceRepo})");
            args.TargetRepo = args.SourceRepo;
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
