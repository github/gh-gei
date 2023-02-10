namespace Octoshift.Models;

// These models only contains the fields relevant for the current GHAS Migration Tasks.
public class CodeScanningAlert
{
    public int Number { get; set; }
    public string Url { get; set; }
    public string State { get; set; }
    public string DismissedAt { get; set; }
    public string DismissedReason { get; set; }
    public string DismissedComment { get; set; }
    public CodeScanningAlertInstance MostRecentInstance { get; set; }
    public string RuleId { get; set; }
}

public class CodeScanningAlertInstance
{
    public string Ref { get; set; }
    public string CommitSha { get; set; }
    public string Path { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
}
