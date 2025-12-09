using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateCodeScanningAlerts;

public class MigrateCodeScanningAlertsCommand : CommandBase<MigrateCodeScanningAlertsCommandArgs, MigrateCodeScanningAlertsCommandHandler>
{
    public MigrateCodeScanningAlertsCommand() : base(
        name: "migrate-code-scanning-alerts",
        description: "Migrates all code-scanning analyses, alert states and possible dismissed-reasons for the default branch. This let's you migrate the history of code-scanning alerts to the target repository.")
    {
        AddOption(SourceOrg);
        AddOption(SourceRepo);
        AddOption(TargetOrg);
        AddOption(TargetRepo);
        AddOption(TargetApiUrl);

        AddOption(GhesApiUrl);
        AddOption(NoSslVerify);

        AddOption(GithubSourcePat);
        AddOption(GithubTargetPat);
        AddOption(Verbose);
        AddOption(DryRun);
    }

    public Option<string> SourceOrg { get; } = new("--source-org") { IsRequired = true };
    public Option<string> SourceRepo { get; } = new("--source-repo") { IsRequired = true };
    public Option<string> TargetOrg { get; } = new("--target-org") { IsRequired = true };
    public Option<string> TargetRepo { get; } = new("--target-repo")
    {
        Description = "Defaults to the name of source-repo"
    };
    public Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        Description =
            "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
    };
    public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
    {
        Description =
            "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
    };
    public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify")
    {
        Description =
            "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
    };
    public Option<string> GithubSourcePat { get; } = new("--github-source-pat")
    {
        Description = "Personal access token of the GitHub source. Overrides GH_SOURCE_PAT environment variable. If neither are set will use the value of the target PAT."
    };
    public Option<string> GithubTargetPat { get; } = new("--github-target-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };
    public Option<bool> Verbose { get; } = new("--verbose");
    public Option<bool> DryRun { get; } = new("--dry-run")
    {
        Description =
            "Execute in dry run mode to see what alerts will be matched and changes applied, but do not make any actual changes."
    };

    public override MigrateCodeScanningAlertsCommandHandler BuildHandler(MigrateCodeScanningAlertsCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        // The factory handles environment variable resolution
        if (args.GithubSourcePat.IsNullOrWhiteSpace())
        {
            args.GithubSourcePat = args.GithubTargetPat;
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var codeScanningAlertServiceFactory = sp.GetRequiredService<CodeScanningAlertServiceFactory>();
        var codeScanningAlertService = codeScanningAlertServiceFactory.Create(args.GhesApiUrl, args.GithubSourcePat, args.TargetApiUrl, args.GithubTargetPat, args.NoSslVerify);

        return new MigrateCodeScanningAlertsCommandHandler(log, codeScanningAlertService);
    }
}
