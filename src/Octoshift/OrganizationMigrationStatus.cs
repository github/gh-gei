namespace OctoshiftCLI
{
    public static class OrganizationMigrationStatus
    {
        public const string Queued = "QUEUED";
        public const string InProgress = "IN_PROGRESS";
        public const string Failed = "FAILED";
        public const string Succeeded = "SUCCEEDED";
        public const string NotStarted = "NOT_STARTED";
        public const string PostRepoMigration = "POST_REPO_MIGRATION";
        public const string PreRepoMigration = "PRE_REPO_MIGRATION";
        public const string RepoMigration = "REPO_MIGRATION";

        public static bool IsSucceeded(string migrationState) => migrationState?.Trim().ToUpper() is Succeeded;
        public static bool IsPending(string migrationState) => migrationState?.Trim().ToUpper() is Queued or InProgress or NotStarted or PostRepoMigration or PreRepoMigration or RepoMigration;
        public static bool IsFailed(string migrationState) => !(IsPending(migrationState) || IsSucceeded(migrationState));
        public static bool IsRepoMigration(string migrationState) => migrationState?.Trim().ToUpper() is RepoMigration;
    }
}
