using OctoshiftCLI.GithubEnterpriseImporter.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories
{
    public class GhesVersionCheckerFactory
    {
        private readonly OctoLogger _log;

        public GhesVersionCheckerFactory(OctoLogger log) => _log = log;

        public virtual GhesVersionChecker Create(GithubApi githubApi)
        {
            return new GhesVersionChecker(_log, githubApi);
        }
    }
}
