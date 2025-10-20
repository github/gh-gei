namespace OctoshiftCLI.Models;
using System.Collections.Generic;


public enum AdoPolicyType { MinimumReviewers, BuildValidation, WorkItemLinking, CommentResolution }

public class AdoPolicyConfiguration
{
    public AdoPolicyType Type { get; init; }
    public bool Enabled { get; init; } = true;
    public int? MinimumApproverCount { get; init; }
    public string BuildDefinitionName { get; init; }
    public string StatusCheckContext { get; init; }
    public IReadOnlyList<string> RegexTemplates { get; init; } = System.Array.Empty<string>();
}
