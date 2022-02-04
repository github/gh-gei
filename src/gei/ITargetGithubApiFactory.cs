namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ITargetGithubApiFactory
    {
        GithubApi Create(string baseUrl);
    }
}
