namespace Octoshift.Models;
public class AbortMigrationResult : GraphqlResult<AbortMigrationData>
{
}

public class AbortMigrationData
{
    public AbortMigrationClass AbortMigrationClass { get; set; }
}

public class AbortMigrationClass
{
    public AbortMigrationData AbortMigrationData { get; set; }
}

public class AbortMigrationData
{
    public AbortRepositoryMigration AbortRepositoryMigration { get; set; }
}

public class AbortRepositoryMigration
{
    public bool Success { get; set; }
}