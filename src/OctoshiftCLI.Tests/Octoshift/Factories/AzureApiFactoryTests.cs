using System.Net.Http;
using Azure.Storage.Blobs;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Factories;

public class AzureApiFactoryTests
{
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient = new Mock<BlobServiceClient>();
    private readonly Mock<IBlobServiceClientFactory> _mockBlobServiceClientFactory = new Mock<IBlobServiceClientFactory>();

    private readonly IAzureApiFactory _azureApiFactory;

    public AzureApiFactoryTests() => _azureApiFactory = new AzureApiFactory(
        _mockHttpClientFactory.Object,
        _mockEnvironmentVariableProvider.Object,
        _mockBlobServiceClientFactory.Object,
        TestHelpers.CreateMock<OctoLogger>().Object);

    [Fact]
    public void AzureApiFactory_Should_Create_With_NoSSL()
    {
        // Arrange
        var _connectionString = "DefaultEndpointsProtocol=https;AccountName=fakename;AccountKey=fakeaccount+hashnumber123;EndpointSuffix=core.windows.net";

        _mockEnvironmentVariableProvider
            .Setup(m => m.AzureStorageConnectionString(It.IsAny<bool>()))
            .Returns(_connectionString);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("NoSSL"))
            .Returns(httpClient);

        _mockBlobServiceClientFactory.Setup(x => x.Create(_connectionString)).Returns(_mockBlobServiceClient.Object);

        // Act
        var azureApi = _azureApiFactory.CreateClientNoSsl(_connectionString);

        // Assert
        azureApi.Should().NotBeNull();
    }

    [Fact]
    public void AzureApiFactory_Should_Create()
    {
        // Arrange
        var _connectionString = "DefaultEndpointsProtocol=https;AccountName=fakename;AccountKey=fakeaccount+hashnumber123;EndpointSuffix=core.windows.net";

        _mockEnvironmentVariableProvider
            .Setup(m => m.AzureStorageConnectionString(It.IsAny<bool>()))
            .Returns(_connectionString);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        _mockBlobServiceClientFactory
            .Setup(x => x.Create(_connectionString))
            .Returns(_mockBlobServiceClient.Object);

        // Act
        var azureApi = _azureApiFactory.Create(_connectionString);

        // Assert
        azureApi.Should().NotBeNull();
    }
}
