using System;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.Handlers;

public class CreateTeamCommandHandler : ICommandHandler<CreateTeamCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public CreateTeamCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
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

        _log.RegisterSecret(args.GithubPat);

        var teams = await _githubApi.GetTeams(args.GithubOrg);
        if (teams.Contains(args.TeamName))
        {
            _log.LogSuccess($"Team '{args.TeamName}' already exists - New team will not be created");
        }
        else
        {
            await _githubApi.CreateTeam(args.GithubOrg, args.TeamName);
            _log.LogSuccess("Successfully created team");
        }

        // TODO: Can improve perf by capturing slug in the response from CreateTeam or GetTeams
        var teamSlug = await _githubApi.GetTeamSlug(args.GithubOrg, args.TeamName);

        if (string.IsNullOrWhiteSpace(args.IdpGroup))
        {
            _log.LogInformation("No IdP Group provided, skipping the IdP linking step");
        }
        else
        {
            var members = await _githubApi.GetTeamMembers(args.GithubOrg, teamSlug);

            foreach (var member in members)
            {
                await _githubApi.RemoveTeamMember(args.GithubOrg, teamSlug, member);
            }

            var idpGroupId = await _githubApi.GetIdpGroupId(args.GithubOrg, args.IdpGroup);

            await _githubApi.AddEmuGroupToTeam(args.GithubOrg, teamSlug, idpGroupId);

            _log.LogSuccess("Successfully linked team to Idp group");
        }
    }
}
