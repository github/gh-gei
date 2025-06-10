using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateDependabotAlerts;

public class MigrateDependabotAlertsCommand : CommandBase<MigrateDependabotAlertsCommandArgs, MigrateDependabotAlertsCommandHandler>
{
    public MigrateDependabotAlertsCommand() : base(
        name: "migrate-dependabot-alerts",
        description: "Migrates Dependabot alert states and dismissed-reasons. This lets you migrate the dismissal state of Dependabot alerts to the target repository.")
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
        Description = "The URL of the target GitHub instance API. Defaults to https://api.github.com"
    };
    public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
    {
        Description =
            "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
    };
    public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify");

    public Option<string> GithubSourcePat { get; } = new("--github-source-pat")
    {
        Description = "Personal access token to use when calling the GitHub source API. Defaults to GH_SOURCE_PAT environment variable."
    };

    public Option<string> GithubTargetPat { get; } = new("--github-target-pat")
    {
        Description = "Personal access token to use when calling the GitHub target API. Defaults to GH_PAT environment variable."
    };

    public Option<bool> Verbose { get; } = new("--verbose");

    public Option<bool> DryRun { get; } = new("--dry-run")
    {
        Description =
            "Execute in dry run mode to see what alerts will be matched and changes applied, but do not make any actual changes."
    };

    public override MigrateDependabotAlertsCommandHandler BuildHandler(MigrateDependabotAlertsCommandArgs args, IServiceProvider sp)
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
        var dependabotAlertServiceFactory = sp.GetRequiredService<DependabotAlertServiceFactory>();
        var dependabotAlertService = dependabotAlertServiceFactory.Create(args.GhesApiUrl, args.GithubSourcePat, args.TargetApiUrl, args.GithubTargetPat, args.NoSslVerify);

        return new MigrateDependabotAlertsCommandHandler(log, dependabotAlertService);
    }
}