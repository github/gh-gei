using System;
using System.Net.Http;
using System.Reflection;
using Azure.Storage.Blobs;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories
{
    public sealed class AzureApiFactory
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly object _blobServiceClientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly OctoLogger _octoLogger;

        public AzureApiFactory(
            IHttpClientFactory clientFactory,
            EnvironmentVariableProvider environmentVariableProvider,
            object blobServiceClientFactory,
            OctoLogger octoLogger)
        {
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _blobServiceClientFactory = blobServiceClientFactory;
            _octoLogger = octoLogger;
        }

        public AzureApi Create(string azureStorageConnectionString = null)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString)
                ? _environmentVariableProvider.AzureStorageConnectionString()
                : azureStorageConnectionString;

            var method = _blobServiceClientFactory.GetType()
                .GetMethod("Create", new[] { typeof(string) });
            var blobServiceClient = (BlobServiceClient)
                method.Invoke(_blobServiceClientFactory, new object[] { connectionString });

            return new AzureApi(
                _clientFactory.CreateClient("Default"),
                blobServiceClient,
                _octoLogger);
        }

        public AzureApi CreateClientNoSsl(string azureStorageConnectionString)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString)
                ? _environmentVariableProvider.AzureStorageConnectionString()
                : azureStorageConnectionString;

            var method = _blobServiceClientFactory.GetType()
                .GetMethod("Create", new[] { typeof(string) });
            var blobServiceClient = (BlobServiceClient)
                method.Invoke(_blobServiceClientFactory, new object[] { connectionString });

            return new AzureApi(
                _clientFactory.CreateClient("NoSSL"),
                blobServiceClient,
                _octoLogger);
        }
    }
}