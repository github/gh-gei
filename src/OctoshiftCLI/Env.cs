using System;

namespace OctoshiftCLI
{
    public static class Env
    {
        private const string GitHubPatKey = "GH_PAT";
        private const string AdoPatKey = "ADO_PAT";
        
        public static string GitHubPersonalAcessToken => Environment.GetEnvironmentVariable(GitHubPatKey);

        public static string AdoPersonalAcessToken => Environment.GetEnvironmentVariable(AdoPatKey);
    }
}