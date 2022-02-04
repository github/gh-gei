namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create(string baseUrl);
        GithubApi CreateClientNoSSL(string baseUrl);
    }
}
