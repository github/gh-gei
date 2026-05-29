namespace OctoshiftCLI.GitlabToGithub;

public static class ExportState
{
    public const string FINISHED = "finished";
    public const string FAILED = "failed";

    public static bool IsInProgress(string state) => state is not FINISHED && !IsError(state);

    public static bool IsError(string state) => state is FAILED;
}
