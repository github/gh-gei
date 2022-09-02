using Octoshift;

namespace OctoshiftCLI.GithubEnterpriseImporter;

public sealed class GitHubSecretScanningAlertServiceFactory: ISecretScanningAlertServiceFactory
{
    private readonly OctoLogger _octoLogger;

    public GitHubSecretScanningAlertServiceFactory(OctoLogger octoLogger)
    {
        _octoLogger = octoLogger;
    }
    
    public SecretScanningAlertService Create(GithubApi sourceApi, GithubApi targetApi) => new (sourceApi, targetApi, _octoLogger);
}
