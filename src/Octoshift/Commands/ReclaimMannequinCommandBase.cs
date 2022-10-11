using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Octoshift;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

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

    protected virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable or --github-pat arg."
    };

    protected virtual Option<string> Csv { get; } = new("--csv")
    {
        Description = "CSV file path with list of mannequins to be reclaimed."
    };

    protected virtual Option<string> MannequinUsername { get; } = new("--mannequin-user")
    {
        Description = "The login of the mannequin to be remapped."
    };

    protected virtual Option<string> MannequinId { get; } = new("--mannequin-id")
    {
        Description = "The Id of the mannequin, in case there are multiple mannequins with the same login you can specify the id to reclaim one of the mannequins."
    };

    protected virtual Option<string> TargetUsername { get; } = new("--target-user")
    {
        Description = "The login of the target user to be mapped."
    };

    protected virtual Option<bool> Force { get; } = new("--force")
    {
        Description = "Map the user even if it was previously mapped"
    };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    protected virtual Option<bool> Verbose { get; } = new("--verbose");

    public override ReclaimMannequinCommandHandler BuildHandler(ReclaimMannequinCommandArgs args, ServiceProvider sp)
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
        var githubApi = githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);
        var reclaimService = new ReclaimService(githubApi, log);

        return new ReclaimMannequinCommandHandler(log, githubApi, reclaimService);
    }

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Csv);
        AddOption(MannequinUsername);
        AddOption(MannequinId);
        AddOption(TargetUsername);
        AddOption(Force);
        AddOption(GithubPat);
        AddOption(Verbose);
    }
}

public class ReclaimMannequinCommandArgs
{
    public string GithubOrg { get; set; }
    public string Csv { get; set; }
    public string MannequinUser { get; set; }
    public string MannequinId { get; set; }
    public string TargetUser { get; set; }
    public bool Force { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
