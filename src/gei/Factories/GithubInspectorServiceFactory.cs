using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories;

public class GithubInspectorServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private readonly FileSystemProvider _fileSystemProvider;
    private GithubInspectorService _instance;

    public GithubInspectorServiceFactory(OctoLogger octoLogger, FileSystemProvider fileSystemProvider)
    {
        _octoLogger = octoLogger;
        _fileSystemProvider = fileSystemProvider;
    }

    public virtual GithubInspectorService Create(GithubApi githubApi)
    {
        _instance ??= new(_octoLogger, _fileSystemProvider, githubApi);

        return _instance;
    }
}
