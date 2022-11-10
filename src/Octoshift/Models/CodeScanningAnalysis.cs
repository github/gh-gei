namespace Octoshift.Models
{
    public class CodeScanningAnalysis
    {
        public string Ref { get; set; }
        public string CommitSha { get; set; }
        public string AnalysisKey { get; set; }
        public CodeScanningEnvironment Environment { get; set; }
        public string Category { get; set; }
        public string Error { get; set; }
        public string CreatedAt { get; set; }
        public int ResultsCount { get; set; }
        public int RulesCount { get; set; }
        public int Id { get; set; }
        public string Url { get; set; }
        public string SarifId { get; set; }
        public CodeScanningTool Tool { get; set; }
        public bool Deletable { get; set; }
        public string Warning { get; set; }
    }

    public class CodeScanningEnvironment
    {
        public string Language { get; set; }
    }

    public class CodeScanningTool
    {
        public string Name { get; set; }
#pragma warning disable CA1720
        public string Guid { get; set; }
#pragma warning restore CA1720
        public string Version { get; set; }
    }

}
