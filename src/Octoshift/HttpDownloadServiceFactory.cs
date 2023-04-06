using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories;

public sealed class HttpDownloadServiceFactory
{
    private readonly OctoLogger _log;
    private readonly IHttpClientFactory _clientFactory;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly IVersionProvider _versionProvider;

    public HttpDownloadServiceFactory(OctoLogger log, IHttpClientFactory clientFactory, FileSystemProvider fileSystemProvider, IVersionProvider versionProvider)
    {
        _log = log;
        _clientFactory = clientFactory;
        _fileSystemProvider = fileSystemProvider;
        _versionProvider = versionProvider;
    }

    public HttpDownloadService CreateDefaultWithRedirects()
    {
        var httpClient = _clientFactory.CreateClient();

        return new HttpDownloadService(_log, httpClient, _fileSystemProvider, _versionProvider);
    }

    public HttpDownloadService CreateDefault()
    {
        var httpClient = _clientFactory.CreateClient("Default");

        return new HttpDownloadService(_log, httpClient, _fileSystemProvider, _versionProvider);
    }

    public HttpDownloadService CreateClientNoSsl()
    {
        var httpClient = _clientFactory.CreateClient("NoSSL");

        return new HttpDownloadService(_log, httpClient, _fileSystemProvider, _versionProvider);
    }
}

