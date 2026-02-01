using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories;

public interface IAzureApiFactory
{
    AzureApi Create(string azureStorageConnectionString);
    AzureApi CreateClientNoSsl(string azureStorageConnectionString);
}
