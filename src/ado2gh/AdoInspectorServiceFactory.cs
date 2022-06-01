namespace OctoshiftCLI.AdoToGithub
{
    public class AdoInspectorServiceFactory
    {
        private readonly OctoLogger _octoLogger;

        public AdoInspectorServiceFactory(OctoLogger octoLogger) => _octoLogger = octoLogger;

        public virtual AdoInspectorService Create(AdoApi adoApi) => new(_octoLogger, adoApi);
    }
}
