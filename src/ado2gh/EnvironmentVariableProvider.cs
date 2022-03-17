using System;

namespace OctoshiftCLI.AdoToGithub;

public class EnvironmentVariableProvider
{
    private const string GH_PAT = "GH_PAT";
    private const string ADO_PAT = "ADO_PAT";

    private readonly OctoLogger _logger;

    public EnvironmentVariableProvider(OctoLogger logger)
    {
        _logger = logger;
    }

    public virtual string GithubPersonalAccessToken() => GetSecret(GH_PAT);

    public virtual string AdoPersonalAccessToken() => GetSecret(ADO_PAT);

    private string GetSecret(string secretName)
    {
        var secret = Environment.GetEnvironmentVariable(secretName);
        if (string.IsNullOrEmpty(secret))
            throw new OctoshiftCliException($"{secretName} environment variable is not set.");

        _logger?.RegisterSecret(secret);

        return secret;
    }
}
