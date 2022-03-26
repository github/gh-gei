namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ITargetGithubApiFactory
    {
        GithubApi Create(string apiUrl = Defaults.GithubApiUrl, string targetPersonalAccessToken = null);
    }
}
