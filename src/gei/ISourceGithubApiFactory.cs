namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create();
        GithubApi CreateClientNoSSL();
    }
}
