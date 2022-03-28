namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create(string apiUrl = null, string sourcePersonalAccessToken = null);
        GithubApi CreateClientNoSsl(string apiUrl = null, string sourcePersonalAccessToken = null);
    }
}
