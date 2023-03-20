namespace OctoshiftCLI.Contracts
{
    public interface IHttpDownloadServiceFactory
    {
        HttpDownloadService Create();
        HttpDownloadService CreateDefault();
        HttpDownloadService CreateClientNoSsl();
    }
}

