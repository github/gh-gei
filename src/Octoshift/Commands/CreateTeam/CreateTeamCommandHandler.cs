using System;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.CreateTeam;

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

        _log.LogInformation("Creating GitHub team...");

        var teams = await _githubApi.GetTeams(args.GithubOrg);
        var teamSlug = teams.FirstOrDefault(t => t.Name == args.TeamName).Slug;
        if (teamSlug.HasValue())
        {
            _log.LogSuccess($"Team '{args.TeamName}' already exists. New team will not be created");
        }
        else
        {
            (_, teamSlug) = await _githubApi.CreateTeam(args.GithubOrg, args.TeamName);
            _log.LogSuccess("Successfully created team");
        }

        if (string.IsNullOrWhiteSpace(args.IdpGroup))
        {
            _log.LogInformation("No IdP Group provided, skipping the IdP linking step");
        }
        else
        {
            _log.LogInformation($"Attempting to link team '{args.TeamName}' to IDP group '{args.IdpGroup}'");

            var members = await _githubApi.GetTeamMembers(args.GithubOrg, teamSlug);
            _log.LogInformation($"Found {members.Count()} existing team members to remove before linking to IDP group");

            foreach (var member in members)
            {
                _log.LogInformation($"Removing team member '{member}' from team '{teamSlug}'");
                await _githubApi.RemoveTeamMember(args.GithubOrg, teamSlug, member);
            }

            _log.LogInformation($"Searching for IDP group '{args.IdpGroup}' in organization '{args.GithubOrg}'");

            int idpGroupId;
            try
            {
                idpGroupId = await _githubApi.GetIdpGroupId(args.GithubOrg, args.IdpGroup);
                _log.LogInformation($"Found IDP group '{args.IdpGroup}' with ID: {idpGroupId}");
            }
            catch (OctoshiftCliException ex)
            {
                _log.LogError($"Failed to find IDP group: {ex.Message}");
                throw;
            }

            _log.LogInformation($"Adding IDP group '{args.IdpGroup}' (ID: {idpGroupId}) to team '{teamSlug}'");
            await _githubApi.AddEmuGroupToTeam(args.GithubOrg, teamSlug, idpGroupId);

            _log.LogSuccess("Successfully linked team to Idp group");
        }
    }
}
