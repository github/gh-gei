namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create();
        GithubApi Create(string apiUrl);
        GithubApi CreateClientNoSsl(string apiUrl);
    }
}
