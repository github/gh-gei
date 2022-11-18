namespace OctoshiftCLI
{
    public static class CodeScanningAlerts
    {
        public const string AlertStateOpen = "open";
        public const string AlertStateDismissed = "dismissed";

        public const string DismissedReasonFalsePositive = "false positive";
        public const string DismissedReasonWontFix = "won't fix";
        public const string DismissedReasonUsedInTests = "used in tests";

        public static bool IsOpenOrDismissed(string alertState) => alertState?.Trim().ToLower() is AlertStateOpen or AlertStateDismissed;

        public static bool IsDismissed(string alertState) => alertState?.Trim().ToLower() is AlertStateDismissed;

        public static bool IsValidDismissedReason(string reason) =>
            reason?.Trim().ToLower() is
                DismissedReasonWontFix or DismissedReasonUsedInTests or DismissedReasonFalsePositive;
    }
}
