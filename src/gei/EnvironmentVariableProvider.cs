using System;

namespace OctoshiftCLI.gei
{

    public class EnvironmentVariableProvider
    {
        private const string SOURCE_GH_PAT = "GH_SOURCE_PAT";
        private const string TARGET_GH_PAT = "GH_PAT";

        public virtual string TargetGithubPersonalAccessToken() => Environment.GetEnvironmentVariable(TARGET_GH_PAT) ??
                                                             throw new ArgumentNullException(
                                                                 $"{TARGET_GH_PAT} environment variables is not set.");

        public virtual string SourceGitHubPersonalAccessToken() => Environment.GetEnvironmentVariable(SOURCE_GH_PAT) ??
                                                                   TargetGithubPersonalAccessToken();
    }
}