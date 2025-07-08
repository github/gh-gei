using Azure.Storage.Blobs;

namespace OctoshiftCLI.Factories;

public interface IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString);
}
