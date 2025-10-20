namespace OctoshiftCLI.Models;

public class GithubRulesetDefinition
{
    public string Name { get; init; }
    public string[] TargetPatterns { get; init; } = new string[0];
    public int? RequiredApprovingReviewCount { get; init; }
    public string[] RequiredStatusChecks { get; init; } = new string[0];
    public string Enforcement { get; init; } = "active";
}
