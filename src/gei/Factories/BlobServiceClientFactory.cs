using Azure.Storage.Blobs;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories;

public class BlobServiceClientFactory : IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString)
    {
        return new BlobServiceClient(connectionString);
    }
}
