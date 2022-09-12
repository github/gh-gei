namespace OctoshiftCLI;
public static class SecretScanningAlert
{
    public const string AlertStateOpen = "open";
    public const string AlertStateResolved = "resolved";

    public const string ResolutionFalsePositive = "false_positive";
    public const string ResolutionRevoked = "revoked";
    public const string ResolutionWontFix = "wont_fix";
    public const string ResolutionUsedInTests = "used_in_tests";

    public static bool IsResolved(string alertState) => alertState?.Trim().ToLower() is AlertStateResolved;

    public static bool IsOpen(string alertState) => alertState?.Trim().ToLower() is AlertStateOpen;

    public static bool IsOpenOrResolved(string alertState) =>
        alertState?.Trim().ToLower() is AlertStateOpen or AlertStateResolved;

    public static bool IsValidDismissedReason(string reason) =>
        reason?.Trim().ToLower() is ResolutionFalsePositive or ResolutionRevoked or ResolutionWontFix or ResolutionUsedInTests;
}
