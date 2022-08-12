using System.CommandLine;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class GrantMigratorRoleCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public GrantMigratorRoleCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base("grant-migrator-role")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;

        Description = "Allows an organization admin to grant a USER or TEAM the migrator role for a single GitHub organization. The migrator role allows the role assignee to perform migrations into the target organization.";
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

        actorType = actorType?.ToUpper();
        _log.LogInformation($"{ActorType.GetLogFriendlyName()}: {actorType}");

        if (actorType is "TEAM" or "USER")
        {
            _log.LogInformation("Actor type is valid...");
        }
        else
        {
            _log.LogError("Actor type must be either TEAM or USER.");
            return;
        }

        if (githubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);
        var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
        var success = await githubApi.GrantMigratorRole(githubOrgId, actor, actorType);

        if (success)
        {
            _log.LogSuccess($"Migrator role successfully set for the {actorType} \"{actor}\"");
        }
        else
        {
            _log.LogError($"Migrator role couldn't be set for the {actorType} \"{actor}\"");
        }
    }
}
