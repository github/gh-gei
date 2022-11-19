namespace OctoshiftCLI.Commands;

public class WaitForMigrationCommandArgs : CommandArgs
{
    public string MigrationId { get; set; }
    [Secret]
    public string GithubPat { get; set; }
}
