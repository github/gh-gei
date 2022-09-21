using System.CommandLine;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.Commands;

public class CreateTeamCommandBase : Command
{
    protected CreateTeamCommandBaseHandler BaseHandler { get; init; }

    public CreateTeamCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "create-team",
        description: "Creates a GitHub team and optionally links it to an IdP group.")
    {
        BaseHandler = new CreateTeamCommandBaseHandler(log, githubApiFactory);
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org") { IsRequired = true };

    protected virtual Option<string> TeamName { get; } = new("--team-name") { IsRequired = true };

    protected virtual Option<string> IdpGroup { get; } = new("--idp-group") { IsRequired = false };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat") { IsRequired = false };

    protected virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

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
