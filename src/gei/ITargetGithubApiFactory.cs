namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ITargetGithubApiFactory
    {
        GithubApi Create();
        GithubApi Create(string apiUrl);
    }
}
