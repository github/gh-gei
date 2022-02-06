namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create(string apiUrl);
        GithubApi CreateClientNoSSL(string apiUrl);
    }
}