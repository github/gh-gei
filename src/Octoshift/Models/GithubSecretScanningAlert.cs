namespace Octoshift.Models;
public class GithubSecretScanningAlert
{
    public int Number { get; set; }
    public string State { get; set; }
    public string Resolution { get; set; }
    public string ResolutionComment { get; set; }
    public string SecretType { get; set; }
    public string Secret { get; set; }
    public string ResolverName { get; set; }
}

public class GithubSecretScanningAlertLocation
{
    public string LocationType { get; set; }
    public string Path { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
    public string BlobSha { get; set; }
    public string IssueTitleUrl { get; set; }
    public string IssueBodyUrl { get; set; }
    public string IssueCommentUrl { get; set; }
    public string DiscussionTitleUrl { get; set; }
    public string DiscussionBodyUrl { get; set; }
    public string DiscussionCommentUrl { get; set; }
    public string PullRequestTitleUrl { get; set; }
    public string PullRequestBodyUrl { get; set; }
    public string PullRequestCommentUrl { get; set; }
    public string PullRequestReviewUrl { get; set; }
    public string PullRequestReviewCommentUrl { get; set; }
}
