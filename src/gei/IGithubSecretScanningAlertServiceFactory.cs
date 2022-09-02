using Octoshift;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ISecretScanningAlertServiceFactory
    {
        SecretScanningAlertService Create(GithubApi sourceApi, GithubApi targetApi);
    }
}
