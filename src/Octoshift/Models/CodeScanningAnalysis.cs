namespace Octoshift.Models
{
    public class CodeScanningAnalysis
    {
        public string Ref { get; set; }
        public string CommitSha { get; set; }
        public string CreatedAt { get; set; }
        public long Id { get; set; }
        public string Error { get; set; }
    }
}
