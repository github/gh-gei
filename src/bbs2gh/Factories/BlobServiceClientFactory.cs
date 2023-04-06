using Azure.Storage.Blobs;

namespace OctoshiftCLI.BbsToGithub.Factories;

public class BlobServiceClientFactory : IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString)
    {
        return new BlobServiceClient(connectionString);
    }
}
