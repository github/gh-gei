using Azure.Storage.Blobs;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface IBlobServiceClientFactory
    {
        public BlobServiceClient Create(string connectionString);
    }
}
