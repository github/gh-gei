namespace OctoshiftCLI;

public static class CodeScanningAlert
{
    public const string AlertStateOpen = "open";
    public const string AlertStateDismissed = "dismissed";

    public const string DismissedReasonFalsePositive = "false_positive";
    public const string DismissedReasonWontFix = "wont_fix";
    public const string DismissedReasonUsedInTests = "used_in_tests";

    public static bool IsOpenOrDismissed(string alertState) =>
        alertState?.Trim().ToLower() is AlertStateOpen or AlertStateDismissed;

    public static bool IsDismissed(string alertState) => alertState?.Trim().ToLower() is AlertStateDismissed;

    public static bool IsValidDismissedReason(string reason) =>
        reason?.Trim().ToLower() is DismissedReasonWontFix or DismissedReasonUsedInTests
            or DismissedReasonFalsePositive;
}
