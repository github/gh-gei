namespace Octoshift.Models
{
    public class GithubSecretScanningAlert
    {
        public int Number { get; set; }
        public string CreatedAt { get; set; }
        public string Url { get; set; }
        public string State { get; set; }
        public string Resolution { get; set; }
        public string ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
        public string SecretType { get; set; }
        public string SecretTypeDisplayName { get; set; }
        public string Secret { get; set; }
        public bool PushProtectionBypassed { get; set; }
        public string PushProtectionBypassedAt { get; set; }
        public string PushProtectionBypassedBy { get; set; }
    }

    public class GithubSecretScanningAlertLocation
    {
        public string Type { get; set; }
        public GithubSecretScanningAlertLocationDetails Details { get; set; }
    }

    public class GithubSecretScanningAlertLocationDetails
    {
        public string Path { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string BlobSha { get; set; }
        public string BlobUrl { get; set; }
        public string CommitSha { get; set; }
        public string CommitUrl { get; set; }
    }
}
