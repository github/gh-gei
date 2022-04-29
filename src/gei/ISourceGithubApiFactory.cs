namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISourceGithubApiFactory
    {
        GithubApi Create(string apiUrl, string sourcePersonalAccessToken, string commandName);
        GithubApi CreateClientNoSsl(string apiUrl, string sourcePersonalAccessToken, string commandName);
    }
}
