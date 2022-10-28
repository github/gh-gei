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
    private const string AWS_ACCESS_KEY = "AWS_ACCESS_KEY";
    private const string AWS_SECRET_KEY = "AWS_SECRET_KEY";

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

    public virtual string BbsUsername(bool throwIfNotFound = true) =>
        GetSecret(BBS_USERNAME)
        ?? (throwIfNotFound ? throw new OctoshiftCliException($"{BBS_USERNAME} environment variable is not set.") : null);

    public virtual string BbsPassword(bool throwIfNotFound = true) =>
        GetSecret(BBS_PASSWORD)
        ?? (throwIfNotFound ? throw new OctoshiftCliException($"{BBS_PASSWORD} environment variable is not set.") : null);

    public virtual string AzureStorageConnectionString(bool throwIfNotFound = true) =>
        GetSecret(AZURE_STORAGE_CONNECTION_STRING)
        ?? (throwIfNotFound ? throw new OctoshiftCliException($"{AZURE_STORAGE_CONNECTION_STRING} environment variable is not set.") : null);

    public virtual string AwsSecretKey(bool throwIfNotFound = true) =>
        GetSecret(AWS_SECRET_KEY)
        ?? (throwIfNotFound ? throw new OctoshiftCliException($"{AWS_SECRET_KEY} environment variable is not set.") : null);

    public virtual string AwsAccessKey(bool throwIfNotFound = true) =>
        GetSecret(AWS_ACCESS_KEY)
        ?? (throwIfNotFound ? throw new OctoshiftCliException($"{AWS_ACCESS_KEY} environment variable is not set.") : null);

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
