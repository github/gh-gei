using System;

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

        public static bool IsPending(string migrationState)
        {
            string[] pendingStates = new string[]
            {
                Queued, InProgress, PendingValidation
            };

            return Array.Exists(pendingStates, e => e == migrationState.Trim().ToUpper());
        }
        public static bool IsFailed(string migrationState)
        {
            string[] failedStates = new string[]
            {
                Failed,
                FailedValidation
            };

            return Array.Exists(failedStates, e => e == migrationState.Trim()
                                                     .ToUpper());

        }
    }
}
