namespace OctoshiftCLI.Contracts
{
    public interface IHttpDownloadServiceFactory
    {
        HttpDownloadService Create();
        HttpDownloadService CreateClientNoSsl();
        HttpDownloadService CreateWithRedirectAllowed();
    }
}

