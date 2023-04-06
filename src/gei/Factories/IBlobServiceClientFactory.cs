using Azure.Storage.Blobs;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories;

public interface IBlobServiceClientFactory
{
    public BlobServiceClient Create(string connectionString);
}
