using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Factories;

public class GitlabInspectorServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private GitlabInspectorService _instance;

    public GitlabInspectorServiceFactory(OctoLogger octoLogger) => _octoLogger = octoLogger;

    public virtual GitlabInspectorService Create(GitlabApi gitlabApi)
    {
        _instance ??= new(_octoLogger, gitlabApi);

        return _instance;
    }
}
