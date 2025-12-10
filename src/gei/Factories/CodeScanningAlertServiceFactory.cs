using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories;

public class CodeScanningAlertServiceFactory
{
    private readonly OctoLogger _octoLogger;
    private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public CodeScanningAlertServiceFactory(
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

    public virtual CodeScanningAlertService
        Create(string sourceApi, string sourceToken, string targetApi, string targetToken, bool sourceApiNoSsl = false)
    {
        // Only resolve from environment if tokens are explicitly null
        // Use consistent throwIfNotFound=false to prevent exceptions when CLI args are preferred
        sourceToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken(false);

        targetToken ??= _environmentVariableProvider.TargetGithubPersonalAccessToken(false);

        // Validate that we have tokens after all resolution attempts
        if (string.IsNullOrWhiteSpace(sourceToken))
        {
            throw new OctoshiftCliException("Source GitHub Personal Access Token is required. Provide it via --github-source-pat argument or GH_SOURCE_PAT/GH_PAT environment variable.");
        }

        if (string.IsNullOrWhiteSpace(targetToken))
        {
            throw new OctoshiftCliException("Target GitHub Personal Access Token is required. Provide it via --github-target-pat argument or GH_PAT environment variable.");
        }

        var sourceGithubApi = sourceApiNoSsl
            ? _sourceGithubApiFactory.CreateClientNoSsl(sourceApi, null, sourceToken)
            : _sourceGithubApiFactory.Create(sourceApi, null, sourceToken);

        return new(sourceGithubApi, _targetGithubApiFactory.Create(targetApi, null, targetToken), _octoLogger);
    }
}
