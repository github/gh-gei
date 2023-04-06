using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Factories;

public class AdoInspectorServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private AdoInspectorService _instance;

    public AdoInspectorServiceFactory(OctoLogger octoLogger) => _octoLogger = octoLogger;

    public virtual AdoInspectorService Create(AdoApi adoApi)
    {
        _instance ??= new(_octoLogger, adoApi);

        return _instance;
    }
}
