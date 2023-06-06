using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Factories;

public class BbsInspectorServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private BbsInspectorService _instance;

    public BbsInspectorServiceFactory(OctoLogger octoLogger) => _octoLogger = octoLogger;

    public virtual BbsInspectorService Create(BbsApi bbsApi)
    {
        _instance ??= new(_octoLogger, bbsApi);

        return _instance;
    }
}
