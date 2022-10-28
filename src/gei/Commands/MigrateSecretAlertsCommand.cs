using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class MigrateSecretAlertsCommand : CommandBase<MigrateSecretAlertsCommandArgs, MigrateSecretAlertsCommandHandler>
{
    public MigrateSecretAlertsCommand() : base(
        name: "migrate-secret-alerts",
        description: "Migrates the state and resolution of secret scanning alerts. You must already have run a secret scan on the target repo before running this command.")
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
            "Execute in dry run mode to see what secrets will be matched and changes applied, but do not make any actual changes."
    };

    public override MigrateSecretAlertsCommandHandler BuildHandler(MigrateSecretAlertsCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
        args.GithubSourcePat ??= environmentVariableProvider.SourceGithubPersonalAccessToken(false);
        args.GithubTargetPat ??= environmentVariableProvider.TargetGithubPersonalAccessToken();

        if (args.GithubSourcePat.IsNullOrWhiteSpace())
        {
            args.GithubSourcePat = args.GithubTargetPat;
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var secretScanningAlertServiceFactory = sp.GetRequiredService<SecretScanningAlertServiceFactory>();
        var secretScanningAlertService = secretScanningAlertServiceFactory.Create(args.GhesApiUrl, args.GithubSourcePat, args.TargetApiUrl, args.GithubTargetPat, args.NoSslVerify);

        return new MigrateSecretAlertsCommandHandler(log, secretScanningAlertService);
    }
}

public class MigrateSecretAlertsCommandArgs
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
