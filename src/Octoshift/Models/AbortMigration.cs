namespace Octoshift.Models;
public class AbortMigrationResult: GraphqlResult<AbortMigrationData>
{
}
public class AbortMigrationData
{
    public AbortMigration AbortMigration { get; set; }
}

public class AbortMigration
{
    public Data Data { get; set; }
}

public class Data
{
    public AbortRepositoryMigration AbortRepositoryMigration { get; set; }
}

public class AbortRepositoryMigration
{
    public bool Success { get; set; }
}