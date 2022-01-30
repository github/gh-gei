using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class HttpClientFactory : IHttpClientFactory
    {
        // todo add the name
        public HttpClient CreateClient(string name)
        {
            #pragma warning disable 2000 // We don't want to dispose the handler until the client is disposed
            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            handler.CheckCertificateRevocationList = true;

            return new HttpClient(handler);
        }
    }
}