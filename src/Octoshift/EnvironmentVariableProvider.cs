using System;

namespace OctoshiftCLI;

public class EnvironmentVariableProvider
{
    private const string SOURCE_GH_PAT = "GH_SOURCE_PAT";
    private const string TARGET_GH_PAT = "GH_PAT";
    private const string ADO_PAT = "ADO_PAT";
    private const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";
    private const string AWS_ACCESS_KEY = "AWS_ACCESS_KEY";
    private const string AWS_SECRET_KEY = "AWS_SECRET_KEY";
    private const string BBS_USERNAME = "BBS_USERNAME";
    private const string BBS_PASSWORD = "BBS_PASSWORD";

    private readonly OctoLogger _logger;

    public EnvironmentVariableProvider(OctoLogger logger)
    {
        _logger = logger;
    }

    public virtual string SourceGithubPersonalAccessToken(bool throwIfNotFound = true) =>
        GetSecret(SOURCE_GH_PAT, false) ?? TargetGithubPersonalAccessToken(throwIfNotFound);

    public virtual string TargetGithubPersonalAccessToken(bool throwIfNotFound = true) =>
        GetSecret(TARGET_GH_PAT, throwIfNotFound);

    public virtual string AdoPersonalAccessToken(bool throwIfNotFound = true) =>
        GetSecret(ADO_PAT, throwIfNotFound);

    public virtual string AzureStorageConnectionString(bool throwIfNotFound = true) =>
        GetSecret(AZURE_STORAGE_CONNECTION_STRING, throwIfNotFound);

    public virtual string AwsSecretKey(bool throwIfNotFound = true) =>
        GetSecret(AWS_SECRET_KEY, throwIfNotFound);

    public virtual string AwsAccessKey(bool throwIfNotFound = true) =>
        GetSecret(AWS_ACCESS_KEY, throwIfNotFound);

    public virtual string BbsUsername(bool throwIfNotFound = true) =>
        GetSecret(BBS_USERNAME, throwIfNotFound);

    public virtual string BbsPassword(bool throwIfNotFound = true) =>
        GetSecret(BBS_PASSWORD, throwIfNotFound);

    private string GetSecret(string secretName, bool throwIfNotFound)
    {
        var secret = Environment.GetEnvironmentVariable(secretName);

        if (string.IsNullOrEmpty(secret))
        {
            return throwIfNotFound
                ? throw new OctoshiftCliException($"{secretName} environment variable is not set.")
                : null;
        }

        _logger?.RegisterSecret(secret);

        return secret;
    }
}
