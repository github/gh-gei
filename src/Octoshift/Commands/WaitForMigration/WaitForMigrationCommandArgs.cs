
namespace OctoshiftCLI.Commands.WaitForMigration;

public class WaitForMigrationCommandArgs : CommandArgs
{
    public string MigrationId { get; set; }
    public string GithubPat { get; set; }
}
