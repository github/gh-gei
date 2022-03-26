namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create(string apiUrl = Defaults.GithubApiUrl, string sourcePersonalAccessToken = null);
        GithubApi CreateClientNoSsl(string apiUrl = Defaults.GithubApiUrl, string sourcePersonalAccessToken = null);
    }
}
