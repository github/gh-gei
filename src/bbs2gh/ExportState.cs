namespace OctoshiftCLI.BbsToGithub;

public static class ExportState
{
    public const string COMPLETED = "COMPLETED";
    public const string FAILED = "FAILED";
    public const string ABORTED = "ABORTED";

    public static bool IsInProgress(string state) => state is not COMPLETED && !IsError(state);

    public static bool IsError(string state) => state is FAILED or ABORTED;
}
