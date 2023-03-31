namespace Octoshift.Models;

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
