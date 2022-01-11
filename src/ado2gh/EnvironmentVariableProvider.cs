using System;

namespace OctoshiftCLI;

public class EnvironmentVariableProvider
{
    private const string GH_PAT = "GH_PAT";
    private const string ADO_PAT = "ADO_PAT";

    public virtual string GithubPersonalAccessToken() => Environment.GetEnvironmentVariable(GH_PAT) ??
                                                         throw new ArgumentNullException(
                                                             $"{GH_PAT} environment variables is not set.");

    public virtual string AdoPersonalAccessToken() => Environment.GetEnvironmentVariable(ADO_PAT) ??
                                                      throw new ArgumentNullException(
                                                          $"{ADO_PAT} environment variables is not set.");
}