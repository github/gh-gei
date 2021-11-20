using System.CommandLine;
using System.CommandLine.Invocation;

namespace OctoshiftCLI.Commands;

public class RevokeMigratorRoleCommand : Command
{
    private readonly OctoLogger _log;
    private readonly GithubApiFactory _githubFactory;

    public RevokeMigratorRoleCommand(OctoLogger log, GithubApiFactory githubFactory) : base("revoke-migrator-role")
    {
        _log = log;
        _githubFactory = githubFactory;
        Description = "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.";

        var githubOrg = new Option<string>("--github-org")
        {
            IsRequired = true
        };
        var actor = new Option<string>("--actor")
        {
            IsRequired = true
        };
        var actorType = new Option<string>("--actor-type")
        {
            IsRequired = true
        };
        var verbose = new Option("--verbose")
        {
            IsRequired = false
        };

        AddOption(githubOrg);
        AddOption(actor);
        AddOption(actorType);
        AddOption(verbose);

        Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
    }

    public async Task Invoke(string githubOrg, string actor, string actorType, bool verbose = false)
    {
        _log.Verbose = verbose;

        _log.LogInformation("Granting migrator role ...");
        _log.LogInformation($"GITHUB ORG: {githubOrg}");
        _log.LogInformation($"ACTOR: {actor}");

        actorType = actorType?.ToUpper();
        _log.LogInformation($"ACTOR TYPE: {actorType}");

        actorType = actorType.ToUpper();

        if (actorType is "TEAM" or "USER")
        {
            _log.LogInformation("Actor type is valid...");
        }
        else
        {
            _log.LogError("Actor type must be either TEAM or USER.");
            return;
        }

        using var github = _githubFactory.Create();

        var githubOrgId = await github.GetOrganizationId(githubOrg);
        var revokeMigratorRoleState = await github.RevokeMigratorRole(githubOrgId, actor, actorType);

        if (revokeMigratorRoleState?.Trim().ToUpper() == "TRUE")
        {
            _log.LogSuccess($"Migrator role successfully revoked for the {actorType} \"{actor}\"");
        }
        else
        {
            _log.LogError($"Migrator role couldn't be revoked for the {actorType} \"{actor}\"");
        }
    }
}