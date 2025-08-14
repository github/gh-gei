using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OctoshiftCLI.Models
{
    public class PipelineTestResult
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepoName { get; set; }
        public string PipelineName { get; set; }
        public int PipelineId { get; set; }
        public string PipelineUrl { get; set; }
        public int? BuildId { get; set; }
        public string BuildUrl { get; set; }
        public string Status { get; set; }
        public string Result { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string ErrorMessage { get; set; }
        public bool RewiredSuccessfully { get; set; }
        public bool RestoredSuccessfully { get; set; }
        public TimeSpan? BuildDuration => EndTime.HasValue ? EndTime.Value - StartTime : null;

        public bool IsSuccessful => string.Equals(Result, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(Result, "partiallySucceeded", StringComparison.OrdinalIgnoreCase);
        public bool IsFailed => string.Equals(Result, "failed", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(Result, "canceled", StringComparison.OrdinalIgnoreCase);
        public bool IsCompleted => !string.IsNullOrEmpty(Result);
        public bool IsRunning => string.Equals(Status, "inProgress", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Status, "notStarted", StringComparison.OrdinalIgnoreCase);
    }

    public class PipelineTestSummary
    {
        private readonly List<PipelineTestResult> _results = [];

        public int TotalPipelines { get; set; }
        public int SuccessfulBuilds { get; set; }
        public int FailedBuilds { get; set; }
        public int TimedOutBuilds { get; set; }
        public int ErrorsRewiring { get; set; }
        public int ErrorsRestoring { get; set; }
        public TimeSpan TotalTestTime { get; set; }
        public Collection<PipelineTestResult> Results => new Collection<PipelineTestResult>(_results);

        public double SuccessRate => TotalPipelines > 0 ? (double)SuccessfulBuilds / TotalPipelines * 100 : 0;

        public void AddResult(PipelineTestResult result)
        {
            _results.Add(result);
        }

        public void AddResults(IEnumerable<PipelineTestResult> results)
        {
            _results.AddRange(results);
        }
    }
}
