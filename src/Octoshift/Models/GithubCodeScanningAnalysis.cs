namespace Octoshift.Models;
public class GithubCodeScanningAnalysis
{
    public string Ref { get; set; }
    public string CommitSha { get; set; }
    public string AnalysisKey { get; set; }
    public GithubCodeScanningEnvironment Environment { get; set; }
    public string Category { get; set; }
    public string Error { get; set; }
    public string CreatedAt { get; set; }
    public int ResultsCount { get; set; }
    public int RulesCount { get; set; }
    public int Id { get; set; }
    public string Url { get; set; }
    public string SarifId { get; set; }
    public GithubCodeScanningTool Tool { get; set; }
    public bool Deletable { get; set; }
    public string Warning { get; set; }
}

public class GithubCodeScanningEnvironment
{
    public string Language { get; set; }
}

public class GithubCodeScanningTool
{
    public string Name { get; set; }
    public string Guid { get; set; }
    public string Version { get; set; }
}
