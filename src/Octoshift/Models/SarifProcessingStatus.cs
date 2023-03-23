using System.Collections.ObjectModel;

namespace OctoshiftCLI.Models;

public class SarifProcessingStatus
{
    public const string Failed = "failed";
    public const string Complete = "complete";
    public const string Pending = "pending";

    public string Status { get; set; }
    public Collection<string> Errors { get; init; }

    public static bool IsPending(string status) => status?.Trim().ToLower() is Pending;
    public static bool IsFailed(string status) => status?.Trim().ToLower() is Failed;
}
