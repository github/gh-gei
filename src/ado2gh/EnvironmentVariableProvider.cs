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

    public virtual string GithubPersonalAccessToken() =>
            GetSecret(GH_PAT)
            ?? throw new OctoshiftCliException($"{GH_PAT} environment variable is not set.");

    public virtual string AdoPersonalAccessToken() =>
            GetSecret(ADO_PAT)
            ?? throw new OctoshiftCliException($"{ADO_PAT} environment variable is not set.");
    
    private string GetSecret(string secretName)
    {
        var secret = Environment.GetEnvironmentVariable(secretName);
        
        if (string.IsNullOrEmpty(secret))
        {
            return null;
        }
        
        _logger?.RegisterSecret(secret);
        
        return secret;
    }
}
