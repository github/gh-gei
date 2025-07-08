using System;
using System.Net.Http;
using Azure.Storage.Blobs;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Services
{
    /// <summary>
    /// Shared helper for AzureApiFactory implementation to avoid duplication.
    /// </summary>
    public static class AzureApiFactoryHelper
    {
        public static AzureApi Create(
            IHttpClientFactory clientFactory,
            dynamic blobServiceClientFactory,
            EnvironmentVariableProvider environmentVariableProvider,
            OctoLogger octoLogger,
            string azureStorageConnectionString = null)
        {
            ArgumentNullException.ThrowIfNull(clientFactory);
            ArgumentNullException.ThrowIfNull(environmentVariableProvider);
            ArgumentNullException.ThrowIfNull(octoLogger);

            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString)
                ? environmentVariableProvider.AzureStorageConnectionString()
                : azureStorageConnectionString;

            var blobServiceClient = blobServiceClientFactory.Create(connectionString);

            return new AzureApi(
                clientFactory.CreateClient("Default"),
                blobServiceClient,
                octoLogger);
        }

        public static AzureApi CreateClientNoSsl(
            IHttpClientFactory clientFactory,
            dynamic blobServiceClientFactory,
            EnvironmentVariableProvider environmentVariableProvider,
            OctoLogger octoLogger,
            string azureStorageConnectionString)
        {
            ArgumentNullException.ThrowIfNull(clientFactory);
            ArgumentNullException.ThrowIfNull(environmentVariableProvider);
            ArgumentNullException.ThrowIfNull(octoLogger);

            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString)
                ? environmentVariableProvider.AzureStorageConnectionString()
                : azureStorageConnectionString;

            var blobServiceClient = blobServiceClientFactory.Create(connectionString);

            return new AzureApi(
                clientFactory.CreateClient("NoSSL"),
                blobServiceClient,
                octoLogger);
        }
    }
}