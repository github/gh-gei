using System.CommandLine;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class RevokeMigratorRoleCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public RevokeMigratorRoleCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base("revoke-migrator-role")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;

        Description = "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.";
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org") { IsRequired = true };

    protected virtual Option<string> Actor { get; } = new("--actor") { IsRequired = true };

    protected virtual Option<string> ActorType { get; } = new("--actor-type") { IsRequired = true };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat") { IsRequired = false };

    protected virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Actor);
        AddOption(ActorType);
        AddOption(GithubPat);
        AddOption(Verbose);
    }

    public async Task Handle(string githubOrg, string actor, string actorType, string githubPat = null, bool verbose = false)
    {
        _log.Verbose = verbose;

        _log.LogInformation("Granting migrator role ...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {githubOrg}");
        _log.LogInformation($"{Actor.GetLogFriendlyName()}: {actor}");

        if (githubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        _log.RegisterSecret(githubPat);

        actorType = actorType?.ToUpper();
        _log.LogInformation($"{ActorType.GetLogFriendlyName()}: {actorType}");

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

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);
        var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
        var success = await githubApi.RevokeMigratorRole(githubOrgId, actor, actorType);

        if (success)
        {
            _log.LogSuccess($"Migrator role successfully revoked for the {actorType} \"{actor}\"");
        }
        else
        {
            _log.LogError($"Migrator role couldn't be revoked for the {actorType} \"{actor}\"");
        }
    }
}
