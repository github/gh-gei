namespace OctoshiftCLI.Services;

public class WarningsCountLogger
{
    private readonly OctoLogger _log;

    public WarningsCountLogger(OctoLogger logger)
    {
        _log = logger;
    }

    public void LogWarningsCount(int warningsCount)
    {
        switch (warningsCount)
        {
            case 0:
                break;
            case 1:
                _log.LogWarning("1 warning encountered during this migration");
                break;
            default:
                _log.LogWarning($"{warningsCount} warnings encountered during this migration");
                break;
        }
    }
}
