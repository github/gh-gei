using System;

namespace OctoshiftCLI.gei
{

    public class EnvironmentVariableProvider
    {
        private const string GH_PAT = "GH_PAT";

        public virtual string GithubPersonalAccessToken() => Environment.GetEnvironmentVariable(GH_PAT) ??
                                                             throw new ArgumentNullException(
                                                                 $"{GH_PAT} environment variables is not set.");
    }
}