namespace OctoshiftCLI.BbsToGithub
{
    public interface IAzureApiFactory
    {
        AzureApi Create(string azureStorageConnectionString);
        AzureApi CreateClientNoSsl(string azureStorageConnectionString);
    }
}
