using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories;

public class ReposCsvGeneratorServiceFactory
{
    private readonly OctoLogger _log;

    public ReposCsvGeneratorServiceFactory(OctoLogger log) => _log = log;

    public virtual ReposCsvGeneratorService Create(GithubApi githubApi)
    {
        return new ReposCsvGeneratorService(_log, githubApi);
    }
}
