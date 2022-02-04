namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface IAzureApiFactory
    {
        AzureApi Create(string azureStorageConnectionString);
    }
}