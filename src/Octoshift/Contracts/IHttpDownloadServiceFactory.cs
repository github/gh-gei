namespace OctoshiftCLI.Contracts
{
    public interface IHttpDownloadServiceFactory
    {
        HttpDownloadService Create(string personalAccessToken = null);
        HttpDownloadService CreateClientNoSsl(string personalAccessToken = null);
    }
}

