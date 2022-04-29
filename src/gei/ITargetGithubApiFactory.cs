namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ITargetGithubApiFactory
    {
        GithubApi Create(string targetPersonalAccessToken, string commandName);
        GithubApi Create(string apiUrl, string targetPersonalAccessToken, string commandName);
    }
}
