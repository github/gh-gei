using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class CreateTeamCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public CreateTeamCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "create-team",
        description: "Creates a GitHub team and optionally links it to an IdP group.")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
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

    public async Task Handle(CreateTeamCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Creating GitHub team...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {args.GithubOrg}");
        _log.LogInformation($"{TeamName.GetLogFriendlyName()}: {args.TeamName}");
        _log.LogInformation($"{IdpGroup.GetLogFriendlyName()}: {args.IdpGroup}");

        if (args.GithubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

        var teams = await githubApi.GetTeams(args.GithubOrg);
        if (teams.Contains(args.TeamName))
        {
            _log.LogSuccess($"Team '{args.TeamName}' already exists - New team will not be created");
        }
        else
        {
            await githubApi.CreateTeam(args.GithubOrg, args.TeamName);
            _log.LogSuccess("Successfully created team");
        }

        // TODO: Can improve perf by capturing slug in the response from CreateTeam or GetTeams
        var teamSlug = await githubApi.GetTeamSlug(args.GithubOrg, args.TeamName);

        if (string.IsNullOrWhiteSpace(args.IdpGroup))
        {
            _log.LogInformation("No IdP Group provided, skipping the IdP linking step");
        }
        else
        {
            var members = await githubApi.GetTeamMembers(args.GithubOrg, teamSlug);

            foreach (var member in members)
            {
                await githubApi.RemoveTeamMember(args.GithubOrg, teamSlug, member);
            }

            var idpGroupId = await githubApi.GetIdpGroupId(args.GithubOrg, args.IdpGroup);

            await githubApi.AddEmuGroupToTeam(args.GithubOrg, teamSlug, idpGroupId);

            _log.LogSuccess("Successfully linked team to Idp group");
        }
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
