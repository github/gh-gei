namespace OctoshiftCLI
{
    public static class RepositoryMigrationStatus
    {
        public const string Queued = "QUEUED";
        public const string InProgress = "IN_PROGRESS";
        public const string Failed = "FAILED";
        public const string Succeeded = "SUCCEEDED";
        public const string PendingValidation = "PENDING_VALIDATION";
        public const string FailedValidation = "FAILED_VALIDATION";

        public static bool IsSucceeded(string migrationState) => migrationState?.Trim().ToUpper() is Succeeded;
        public static bool IsPending(string migrationState) => migrationState?.Trim().ToUpper() is Queued or InProgress or PendingValidation;
        public static bool IsFailed(string migrationState) => !(IsPending(migrationState) || IsSucceeded(migrationState));
    }
}
