using System;

namespace OctoshiftCLI.gei;

public class EnvironmentVariableProvider
{
    private const string GH_PAT = "GH_PAT";
    private const string GH_SOURCE_PAT = "GH_SOURCE_PAT";

    public virtual string GithubTargetPersonalAccessToken() => Environment.GetEnvironmentVariable(GH_PAT) ??
                                                         throw new ArgumentNullException(
                                                             $"{GH_PAT} environment variables is not set.");

    public virtual string GithubSourcePersonalAccessToken() => Environment.GetEnvironmentVariable(GH_SOURCE_PAT) ??
                                                      throw new ArgumentNullException(
                                                          $"{GH_SOURCE_PAT} environment variables is not set.");
}