namespace OctoshiftCLI
{
    public static class CodeScanningAlertState
    {
        public const string Open = "open";
        public const string Dismissed = "dismissed";

        public const string FalsePositive = "false positive";
        public const string WontFix = "won't fix";
        public const string UsedInTests = "used in tests";

        public static bool IsOpenOrDismissed(string state) => state?.Trim().ToLower() is Open or Dismissed;

        public static bool IsDismissed(string state) => state?.Trim().ToLower() is Dismissed;

        public static bool IsValidDismissedReason(string reason) =>
            reason?.Trim().ToLower() is
                WontFix or UsedInTests or FalsePositive;
    }
}
