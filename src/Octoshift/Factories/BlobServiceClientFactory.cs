using Azure.Storage.Blobs;

namespace OctoshiftCLI.Factories;

public class BlobServiceClientFactory : IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString)
    {
        return new BlobServiceClient(connectionString);
    }
}
