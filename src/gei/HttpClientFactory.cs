using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class HttpClientFactory : IHttpClientFactory
    {
        // todo add the name
        public HttpClient CreateClient(string name)
        {
#pragma warning disable CA2000 // We don't want to dispose the handler until the client is disposed
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = false,

                // ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                // ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
            };

            // handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            return new HttpClient(handler);
        }
    }
}