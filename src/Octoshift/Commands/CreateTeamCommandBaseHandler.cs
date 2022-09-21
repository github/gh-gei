using System;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.Commands;

public class CreateTeamCommandBaseHandler
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public CreateTeamCommandBaseHandler(OctoLogger log, ITargetGithubApiFactory githubApiFactory)
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
    }

    public async Task Handle(CreateTeamCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Creating GitHub team...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"TEAM NAME: {args.TeamName}");
        _log.LogInformation($"IDP GROUP: {args.IdpGroup}");

        if (args.GithubPat is not null)
        {
            _log.LogInformation($"GITHUB PAT: ***");
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
