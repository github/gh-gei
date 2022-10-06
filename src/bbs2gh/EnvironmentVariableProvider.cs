using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("OctoshiftCLI.Tests")]

namespace OctoshiftCLI.BbsToGithub;

public class EnvironmentVariableProvider
{
    private const string GH_PAT = "GH_PAT";
    private const string BBS_USERNAME = "BBS_USERNAME";
    private const string BBS_PASSWORD = "BBS_PASSWORD";
    private const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";

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

    public virtual string BbsUsername() =>
            GetSecret(BBS_USERNAME)
            ?? throw new OctoshiftCliException($"{BBS_USERNAME} environment variable is not set.");

    public virtual string BbsPassword() =>
            GetSecret(BBS_PASSWORD)
            ?? throw new OctoshiftCliException($"{BBS_PASSWORD} environment variable is not set.");

    public virtual string AzureStorageConnectionString(bool throwIfNotFound = true) =>
        GetSecret(AZURE_STORAGE_CONNECTION_STRING)
        ?? (throwIfNotFound ? throw new OctoshiftCliException($"{AZURE_STORAGE_CONNECTION_STRING} environment variable is not set.") : null);

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
