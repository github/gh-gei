using Azure.Storage.Blobs;

namespace OctoshiftCLI.BbsToGithub;

public interface IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString);
}
