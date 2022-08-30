using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.BbsToGithub;

public class EnvironmentVariableProvider
{
    private const string GH_PAT = "GH_PAT";
    private const string BBS_SERVER_URL = "BBS_SERVER_URL";
    private const string BBS_USERNAME = "BBS_USERNAME";
    private const string BBS_PASSWORD = "BBS_PASSWORD";

    private readonly OctoLogger _logger;

    private readonly Func<string, string> _getEnvironmentVariable;

    public EnvironmentVariableProvider(OctoLogger logger) : this(logger, v => Environment.GetEnvironmentVariable(v))
    {
    }

    internal EnvironmentVariableProvider(OctoLogger logger, Func<string, string> getEnvironmentVariable)
    {
        _logger = logger;
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    public virtual string GithubPersonalAccessToken() =>
            GetSecret(GH_PAT)
            ?? throw new OctoshiftCliException($"{GH_PAT} environment variable is not set.");

    public virtual string BbsServerUrl() =>
            GetSecret(BBS_SERVER_URL)
            ?? throw new OctoshiftCliException($"{BBS_SERVER_URL} environment variable is not set.");

    public virtual string BbsUsername() =>
            GetSecret(BBS_USERNAME)
            ?? throw new OctoshiftCliException($"{BBS_USERNAME} environment variable is not set.");

    public virtual string BbsPassword() =>
            GetSecret(BBS_PASSWORD)
            ?? throw new OctoshiftCliException($"{BBS_PASSWORD} environment variable is not set.");

    private string GetSecret(string secretName)
    {
        var secret = _getEnvironmentVariable(secretName);

        if (string.IsNullOrEmpty(secret))
        {
            return null;
        }

        _logger?.RegisterSecret(secret);

        return secret;
    }
}
