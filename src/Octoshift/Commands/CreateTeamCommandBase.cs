using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

public class CreateTeamCommandBase : CommandBase<CreateTeamCommandArgs, CreateTeamCommandHandler>
{
    public CreateTeamCommandBase() : base(name: "create-team", description: "Creates a GitHub team and optionally links it to an IdP group.")
    {
    }

    public virtual Option<string> GithubOrg { get; } = new("--github-org") { IsRequired = true };

    public virtual Option<string> TeamName { get; } = new("--team-name") { IsRequired = true };

    public virtual Option<string> IdpGroup { get; } = new("--idp-group");

    public virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

    public override CreateTeamCommandHandler BuildHandler(CreateTeamCommandArgs args, IServiceProvider sp)
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

        return new CreateTeamCommandHandler(log, githubApi);
    }

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(TeamName);
        AddOption(IdpGroup);
        AddOption(GithubPat);
        AddOption(Verbose);
    }
}

public class CreateTeamCommandArgs
{
    public string GithubOrg { get; set; }
    public string TeamName { get; set; }
    public string IdpGroup { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
