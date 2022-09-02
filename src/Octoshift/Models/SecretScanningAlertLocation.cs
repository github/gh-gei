namespace Octoshift.Models
{
    public class SecretScanningAlertLocation
    {
        public string Type { get; set; }
        public SecretScanningAlertLocationDetails Details { get; set; }
    }

    public class SecretScanningAlertLocationDetails
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
