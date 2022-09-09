using Octoshift;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter;

public sealed class GitHubSecretScanningAlertServiceFactory : ISecretScanningAlertServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;

    public GitHubSecretScanningAlertServiceFactory(OctoLogger octoLogger,
        ISourceGithubApiFactory sourceGithubApiFactory, ITargetGithubApiFactory targetGithubApiFactory)
    {
        _octoLogger = octoLogger;
        _sourceGithubApiFactory = sourceGithubApiFactory;
        _targetGithubApiFactory = targetGithubApiFactory;
    }

    public SecretScanningAlertService
        Create(string sourceApi, string sourceToken, string targetApi, string targetToken, bool sourceApiNoSsl = false)
    {
        var sourceGithubApi = sourceApiNoSsl
            ? _sourceGithubApiFactory.CreateClientNoSsl(sourceApi, sourceToken)
            : _sourceGithubApiFactory.Create(sourceApi, sourceToken);

        return new(sourceGithubApi, _targetGithubApiFactory.Create(targetApi, targetToken), _octoLogger);
    }
}
