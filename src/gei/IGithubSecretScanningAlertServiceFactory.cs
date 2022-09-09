using Octoshift;

namespace OctoshiftCLI.GithubEnterpriseImporter;
public interface ISecretScanningAlertServiceFactory
{
    SecretScanningAlertService Create(string sourceApi, string sourceToken, string targetApi, string targetToken, bool sourceApiNoSsl = false);
}
