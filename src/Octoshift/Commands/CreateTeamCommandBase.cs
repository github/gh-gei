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

    public CreateTeamCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base("create-team")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;

        Description = "Creates a GitHub team and optionally links it to an IdP group.";
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

    public async Task Handle(string githubOrg, string teamName, string idpGroup, string githubPat = null, bool verbose = false)
    {
        _log.Verbose = verbose;

        _log.LogInformation("Creating GitHub team...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {githubOrg}");
        _log.LogInformation($"{TeamName.GetLogFriendlyName()}: {teamName}");
        _log.LogInformation($"{IdpGroup.GetLogFriendlyName()}: {idpGroup}");

        if (githubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);

        var teams = await githubApi.GetTeams(githubOrg);
        if (teams.Contains(teamName))
        {
            _log.LogSuccess($"Team '{teamName}' already exists - New team will not be created");
        }
        else
        {
            await githubApi.CreateTeam(githubOrg, teamName);
            _log.LogSuccess("Successfully created team");
        }

        // TODO: Can improve perf by capturing slug in the response from CreateTeam or GetTeams
        var teamSlug = await githubApi.GetTeamSlug(githubOrg, teamName);

        if (string.IsNullOrWhiteSpace(idpGroup))
        {
            _log.LogInformation("No IdP Group provided, skipping the IdP linking step");
        }
        else
        {
            var members = await githubApi.GetTeamMembers(githubOrg, teamSlug);

            foreach (var member in members)
            {
                await githubApi.RemoveTeamMember(githubOrg, teamSlug, member);
            }

            var idpGroupId = await githubApi.GetIdpGroupId(githubOrg, idpGroup);

            await githubApi.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId);

            _log.LogSuccess("Successfully linked team to Idp group");
        }
    }
}
