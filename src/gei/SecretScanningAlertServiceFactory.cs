using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter;

public class SecretScanningAlertServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public SecretScanningAlertServiceFactory(
        OctoLogger octoLogger,
        ISourceGithubApiFactory sourceGithubApiFactory,
        ITargetGithubApiFactory targetGithubApiFactory,
        EnvironmentVariableProvider environmentVariableProvider)
    {
        _octoLogger = octoLogger;
        _sourceGithubApiFactory = sourceGithubApiFactory;
        _targetGithubApiFactory = targetGithubApiFactory;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual SecretScanningAlertService
        Create(string sourceApi, string sourceToken, string targetApi, string targetToken, bool sourceApiNoSsl = false)
    {
        sourceToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
        targetToken ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();

        var sourceGithubApi = sourceApiNoSsl
            ? _sourceGithubApiFactory.CreateClientNoSsl(sourceApi, sourceToken)
            : _sourceGithubApiFactory.Create(sourceApi, sourceToken);

        return new(sourceGithubApi, _targetGithubApiFactory.Create(targetApi, targetToken), _octoLogger);
    }
}
