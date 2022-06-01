namespace OctoshiftCLI.AdoToGithub
{
    public class AdoInspectorServiceFactory
    {
        private readonly OctoLogger _octoLogger;
        private AdoInspectorService _instance;

        public AdoInspectorServiceFactory(OctoLogger octoLogger) => _octoLogger = octoLogger;

        public virtual AdoInspectorService Create(AdoApi adoApi)
        {
            if (_instance is null)
            {
                _instance = new(_octoLogger, adoApi);
            }

            return _instance;
        }
    }
}
