namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ITargetGithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";
        GithubApi Create(string apiUrl = DEFAULT_API_URL, string targetPersonalAccessToken = null);
    }
}
