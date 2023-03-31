namespace Octoshift.Models;

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
