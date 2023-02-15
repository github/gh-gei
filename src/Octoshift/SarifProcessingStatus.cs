namespace OctoshiftCLI
{
    public static class SarifProcessingStatus
    {
        public const string Failed = "failed";
        public const string Complete = "complete";
        public const string Pending = "pending";
        
        public static bool IsComplete(string status) => status?.Trim().ToLower() is Complete;
        public static bool IsPending(string status) => status?.Trim().ToLower() is Pending;
        public static bool IsFailed(string status) => status?.Trim().ToLower() is Failed;
    }
}

