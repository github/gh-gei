namespace Octoshift.Models;

public class DependabotAlert
{
    public int Number { get; set; }
    public string State { get; set; }
    public string DismissedReason { get; set; }
    public string DismissedComment { get; set; }
    public string DismissedAt { get; set; }
    public DependabotAlertDependency Dependency { get; set; }
    public DependabotAlertSecurityAdvisory SecurityAdvisory { get; set; }
    public DependabotAlertSecurityVulnerability SecurityVulnerability { get; set; }
    public string Url { get; set; }
    public string HtmlUrl { get; set; }
    public string CreatedAt { get; set; }
    public string UpdatedAt { get; set; }
}

public class DependabotAlertDependency
{
    public string Package { get; set; }
    public string Manifest { get; set; }
    public string Scope { get; set; }
}

public class DependabotAlertSecurityAdvisory
{
    public string GhsaId { get; set; }
    public string CveId { get; set; }
    public string Summary { get; set; }
    public string Description { get; set; }
    public string Severity { get; set; }
}

public class DependabotAlertSecurityVulnerability
{
    public string Package { get; set; }
    public string Severity { get; set; }
    public string VulnerableVersionRange { get; set; }
    public string FirstPatchedVersion { get; set; }
}
