namespace OctoshiftCLI.Models;
using System.Collections.Generic;


public class GithubRulesetDefinition
{
    public string Name { get; init; }
    public IReadOnlyList<string> TargetPatterns { get; init; } = System.Array.Empty<string>();
    public int? RequiredApprovingReviewCount { get; init; }
    public IReadOnlyList<string> RequiredStatusChecks { get; init; } = System.Array.Empty<string>();
    public IReadOnlyList<string> RequiredPullRequestBodyPatterns { get; init; } = System.Array.Empty<string>();
    public string Enforcement { get; init; } = "active";
}
