namespace OctoshiftCLI;

public static class DependabotAlertState
{
    public const string Open = "open";
    public const string Dismissed = "dismissed";
    public const string Fixed = "fixed";

    // Dismissal reasons for Dependabot alerts
    public const string FalsePositive = "false_positive";
    public const string Inaccurate = "inaccurate";
    public const string NotUsed = "not_used";
    public const string NoBandwidth = "no_bandwidth";
    public const string TolerableRisk = "tolerable_risk";

    public static bool IsOpenOrDismissed(string state) => state?.Trim().ToLower() is Open or Dismissed;

    public static bool IsDismissed(string state) => state?.Trim().ToLower() is Dismissed;

    public static bool IsValidDismissedReason(string reason) =>
        reason?.Trim().ToLower() is
            FalsePositive or Inaccurate or NotUsed or NoBandwidth or TolerableRisk;
}
