using System.Net.Http;
using Azure.Storage.Blobs;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class AzureApiFactoryTests
    {
        private readonly OctoLogger _logger;

        public AzureApiFactoryTests()
        {
            _logger = TestHelpers.CreateMock<OctoLogger>().Object;
        }

        [Fact]
        public void AzureApiFactory_Should_Create_With_NoSSL()
        {
            // Arrange
            var _connectionString = "DefaultEndpointsProtocol=https;AccountName=fakename;AccountKey=fakeaccount+hashnumber123;EndpointSuffix=core.windows.net";

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.AzureStorageConnectionString())
                .Returns(_connectionString);

            using var httpClient = new HttpClient();

            var _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient("NoSSL"))
                .Returns(httpClient);

            var mockBlob = new Mock<BlobServiceClient>();
            var blob = new Mock<IBlobServiceClientFactory>();
            blob.Setup(x => x.Create(_connectionString)).Returns(mockBlob.Object);

            // Act
            IAzureApiFactory factory =
                new AzureApiFactory(_mockHttpClientFactory.Object, environmentVariableProviderMock.Object, blob.Object);
            var azureApi = factory.CreateClientNoSsl(_connectionString);

            // Assert
            azureApi.Should().NotBeNull();
        }

        [Fact]
        public void AzureApiFactory_Should_Create()
        {
            // Arrange
            var _connectionString = "DefaultEndpointsProtocol=https;AccountName=fakename;AccountKey=fakeaccount+hashnumber123;EndpointSuffix=core.windows.net";

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);

            environmentVariableProviderMock
                .Setup(m => m.AzureStorageConnectionString())
                .Returns(_connectionString);

            using var httpClient = new HttpClient();

            var _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);


            var mockBlob = new Mock<BlobServiceClient>();
            var blob = new Mock<IBlobServiceClientFactory>();
            blob
                .Setup(x => x.Create(_connectionString))
                .Returns(mockBlob.Object);

            // Act
            IAzureApiFactory factory =
                new AzureApiFactory(_mockHttpClientFactory.Object, environmentVariableProviderMock.Object, blob.Object);
            var azureApi = factory.Create(_connectionString);

            // Assert
            azureApi.Should().NotBeNull();
        }
    }
}
