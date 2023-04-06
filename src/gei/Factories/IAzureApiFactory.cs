using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories;

public interface IAzureApiFactory
{
    AzureApi Create(string azureStorageConnectionString);
    AzureApi CreateClientNoSsl(string azureStorageConnectionString);
}
