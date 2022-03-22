namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";
        GithubApi Create(string apiUrl = DEFAULT_API_URL, string sourcePersonalAccessToken = null);
        GithubApi CreateClientNoSsl(string apiUrl = DEFAULT_API_URL, string sourcePersonalAccessToken = null);
    }
}
