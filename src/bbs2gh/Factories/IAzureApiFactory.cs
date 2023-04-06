using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Factories;

public interface IAzureApiFactory
{
    AzureApi Create(string azureStorageConnectionString);
    AzureApi CreateClientNoSsl(string azureStorageConnectionString);
}
