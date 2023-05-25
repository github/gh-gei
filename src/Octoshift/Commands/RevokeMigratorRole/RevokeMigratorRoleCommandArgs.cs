using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.RevokeMigratorRole;

public class RevokeMigratorRoleCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string GhesApiUrl { get; set; }

    public override void Validate(OctoLogger log)
    {
        ActorType = ActorType?.ToUpper();

        if (ActorType is "TEAM" or "USER")
        {
            log?.LogInformation("Actor type is valid...");
        }
        else
        {
            throw new OctoshiftCliException("Actor type must be either TEAM or USER.");
        }
    }
}
