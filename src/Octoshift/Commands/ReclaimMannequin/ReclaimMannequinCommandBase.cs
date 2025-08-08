using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandBase : CommandBase<ReclaimMannequinCommandArgs, ReclaimMannequinCommandHandler>
{
    public ReclaimMannequinCommandBase() : base(
        name: "reclaim-mannequin",
        description: "Reclaims one or more mannequin user(s). An invite will be sent and the user(s) will have to accept for the remapping to occur." +
                     "You can reclaim a single user by using --mannequin-user and --target-user or reclaim mannequins in bulk by using the --csv parameter" +
                     Environment.NewLine +
                     "The CSV file should contain a column with the user's login name (source) and reclaiming user login (target)." +
                     Environment.NewLine +
                     "The first line is considered the header and is ignored." +
                     Environment.NewLine +
                     "If both options are specified The CSV file takes precedence and other options will be ignored")
    {
    }

    public virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable or --github-pat arg."
    };

    public virtual Option<string> Csv { get; } = new("--csv")
    {
        Description = "CSV file path with list of mannequins to be reclaimed."
    };

    public virtual Option<string> MannequinUser { get; } = new("--mannequin-user")
    {
        Description = "The login of the mannequin to be remapped."
    };

    public virtual Option<string> MannequinId { get; } = new("--mannequin-id")
    {
        Description = "The Id of the mannequin, in case there are multiple mannequins with the same login you can specify the id to reclaim one of the mannequins."
    };

    public virtual Option<string> TargetUser { get; } = new("--target-user")
    {
        Description = "The login of the target user to be mapped."
    };

    public virtual Option<bool> Force { get; } = new("--force")
    {
        Description = "Map the user even if it was previously mapped"
    };

    public virtual Option<bool> NoPrompt { get; } = new("--no-prompt")
    {
        Description = "Overrides all prompts and warnings with 'Y' value."
    };

    public virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public virtual Option<bool> SkipInvitation { get; } = new("--skip-invitation")
    {
        Description = "Reclaim mannequins immediately without sending an invitation to the user. Only available for Enterprise Managed Users (EMU) organizations. Warning: this is irreversible!"
    };

    public virtual Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
    };

    public virtual Option<bool> Verbose { get; } = new("--verbose");

    public override ReclaimMannequinCommandHandler BuildHandler(ReclaimMannequinCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var githubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
        var githubApi = githubApiFactory.Create(args.TargetApiUrl, null, args.GithubPat);
        var reclaimService = new ReclaimService(githubApi, log);
        var confirmationService = sp.GetRequiredService<ConfirmationService>();

        return new ReclaimMannequinCommandHandler(log, reclaimService, confirmationService, githubApi);
    }

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Csv);
        AddOption(MannequinUser);
        AddOption(MannequinId);
        AddOption(TargetUser);
        AddOption(Force);
        AddOption(NoPrompt);
        AddOption(GithubPat);
        AddOption(SkipInvitation);
        AddOption(TargetApiUrl);
        AddOption(Verbose);
    }
}
