using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class MigrateSecretScanningAlertsCommand : Command
{
    private readonly OctoLogger _log;

    private readonly GitHubSecretScanningAlertServiceFactory _secretScanningAlertServiceFactory;

    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public MigrateSecretScanningAlertsCommand(OctoLogger log,
        GitHubSecretScanningAlertServiceFactory secretScanningAlertServiceFactory,
        EnvironmentVariableProvider environmentVariableProvider) : base("migrate-secret-alerts")
    {
        _log = log;
        _secretScanningAlertServiceFactory = secretScanningAlertServiceFactory;
        _environmentVariableProvider = environmentVariableProvider;

        Description = "Invokes the GitHub APIs to migrate repo secret scanning alert data.";

        var githubSourceOrg = new Option<string>("--source-org") { IsRequired = true };
        var sourceRepo = new Option<string>("--source-repo") { IsRequired = true };

        var githubTargetOrg = new Option<string>("--target-org") { IsRequired = true };
        var targetRepo = new Option<string>("--target-repo")
        {
            IsRequired = false,
            Description = "Defaults to the name of source-repo"
        };
        var targetApiUrl = new Option<string>("--target-api-url")
        {
            IsRequired = false,
            Description =
                "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };

        // GHES migration path
        var ghesApiUrl = new Option<string>("--ghes-api-url")
        {
            IsRequired = false,
            Description =
                "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
        };
        var noSslVerify = new Option("--no-ssl-verify")
        {
            IsRequired = false,
            Description =
                "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
        };
        var githubSourcePat = new Option<string>("--github-source-pat") { IsRequired = false };
        var githubTargetPat = new Option<string>("--github-target-pat") { IsRequired = false };
        var verbose = new Option("--verbose") { IsRequired = false };
        var dryRun = new Option("--dry-run")
        {
            IsRequired = false,
            Description =
                "Execute in dry run mode to see what secrets will be matched and changes applied, but do not make any actual changes."
        };

        AddOption(githubSourceOrg);
        AddOption(sourceRepo);
        AddOption(githubTargetOrg);
        AddOption(targetRepo);
        AddOption(targetApiUrl);

        AddOption(ghesApiUrl);
        AddOption(noSslVerify);

        AddOption(githubSourcePat);
        AddOption(githubTargetPat);
        AddOption(verbose);
        AddOption(dryRun);

        Handler = CommandHandler.Create<MigrateSecretScanningAlertsCommandArgs>(Invoke);
    }

    public async Task Invoke(MigrateSecretScanningAlertsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        LogAndValidateOptions(args);

        var migrationService = _secretScanningAlertServiceFactory.Create(args.GhesApiUrl, GetSourceToken(args), args.TargetApiUrl, GetTargetToken(args), args.NoSslVerify);

        await migrationService.MigrateSecretScanningAlerts(
            args.SourceOrg,
            args.SourceRepo,
            args.TargetOrg,
            args.TargetRepo,
            args.DryRun);

        _log.LogSuccess($"Secret Scanning results completed.");
    }

    private string GetSourceToken(MigrateSecretScanningAlertsCommandArgs args) =>
        args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

    private string GetTargetToken(MigrateSecretScanningAlertsCommandArgs args) =>
        args.GithubTargetPat ?? _environmentVariableProvider.TargetGithubPersonalAccessToken();

    private void LogAndValidateOptions(MigrateSecretScanningAlertsCommandArgs args)
    {
        _log.LogInformation("Migrating Repo Secret Scanning Alerts...");

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

public class MigrateSecretScanningAlertsCommandArgs
{
    public string SourceOrg { get; set; }
    public string SourceRepo { get; set; }
    public string TargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string TargetApiUrl { get; set; }
    public string GhesApiUrl { get; set; }
    public bool NoSslVerify { get; set; }
    public bool Verbose { get; set; }
    public bool DryRun { get; set; }
    public string GithubSourcePat { get; set; }
    public string GithubTargetPat { get; set; }
}
