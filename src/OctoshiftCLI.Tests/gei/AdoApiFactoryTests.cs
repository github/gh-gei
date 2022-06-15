using System;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class AdoApiFactoryTests
{
    private readonly Mock<EnvironmentVariableProvider> _environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<RetryPolicy> _retryPolicyMock = TestHelpers.CreateMock<RetryPolicy>();

    private const string ADO_PAT = "ADO_PAT";

    [Fact]
    public void AdoApiFactory_Should_Create_Ado_Api_With_Ado_Pat_From_Environment_If_Not_Provided()
    {
        // Arrange
        _environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken()).Returns(ADO_PAT);

        using var httpClient = new HttpClient();

        // Act
        var factory = new AdoApiFactory(null, httpClient, _environmentVariableProviderMock.Object, null, _retryPolicyMock.Object);
        var result = factory.Create(null, null);

        // Assert
        result.Should().NotBeNull();
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{ADO_PAT}"));
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(authToken);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");

        _environmentVariableProviderMock.Verify(m => m.AdoPersonalAccessToken());
    }

    [Fact]
    public void AdoApiFactory_Should_Create_Ado_Api_With_Provided_Ado_Pat()
    {
        // Arrange
        _environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken()).Returns(ADO_PAT);

        using var httpClient = new HttpClient();

        // Act
        var factory = new AdoApiFactory(null, httpClient, _environmentVariableProviderMock.Object, null, _retryPolicyMock.Object);
        var result = factory.Create(null, ADO_PAT);

        // Assert
        result.Should().NotBeNull();
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{ADO_PAT}"));
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(authToken);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");

        _environmentVariableProviderMock.Verify(m => m.AdoPersonalAccessToken(), Times.Never);
    }
}
