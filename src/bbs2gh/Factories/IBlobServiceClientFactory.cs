using Azure.Storage.Blobs;

namespace OctoshiftCLI.BbsToGithub.Factories;

public interface IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString);
}
