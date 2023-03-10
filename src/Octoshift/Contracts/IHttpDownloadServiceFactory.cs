using System;
namespace OctoshiftCLI.Contracts
{
    public interface IHttpDownloadServiceFactory
    {
        HttpDownloadService Create(string apiUrl = null, string sourcePersonalAccessToken = null);
        HttpDownloadService CreateClientNoSsl(string apiUrl = null, string sourcePersonalAccessToken = null);
    }
}

