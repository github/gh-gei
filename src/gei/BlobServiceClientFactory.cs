using Azure.Storage.Blobs;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class BlobServiceClientFactory : IBlobServiceClientFactory
    {

        public BlobServiceClientFactory() { }

        public BlobServiceClient Create(string connectionString)
        {
            return new BlobServiceClient(connectionString);
        }
    }
}
