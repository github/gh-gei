using System;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Factories;

public class AdoApiFactoryTests
{
    private const string ADO_PAT = "ADO_PAT";
    private readonly Mock<RetryPolicy> _retryPolicyMock = TestHelpers.CreateMock<RetryPolicy>();

    [Fact]
    public void Create_Should_Create_Ado_Api_With_Ado_Pat_From_Environment_If_Not_Provided()
    {
        // Arrange
        var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken(It.IsAny<bool>())).Returns(ADO_PAT);

        using var httpClient = new HttpClient();

        // Act
        var factory = new AdoApiFactory(null, httpClient, environmentVariableProviderMock.Object, null, _retryPolicyMock.Object);
        var result = factory.Create(null);

        // Assert
        result.Should().NotBeNull();

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{ADO_PAT}"));
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(authToken);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");

        environmentVariableProviderMock.Verify(m => m.AdoPersonalAccessToken(It.IsAny<bool>()));
    }

    [Fact]
    public void Create_Should_Create_Ado_Api_With_Provided_Ado_Pat()
    {
        // Arrange
        var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken(It.IsAny<bool>())).Returns(ADO_PAT);

        using var httpClient = new HttpClient();

        // Act
        var factory = new AdoApiFactory(null, httpClient, environmentVariableProviderMock.Object, null, _retryPolicyMock.Object);
        var result = factory.Create(ADO_PAT);

        // Assert
        result.Should().NotBeNull();

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{ADO_PAT}"));
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(authToken);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");

        environmentVariableProviderMock.Verify(m => m.AdoPersonalAccessToken(It.IsAny<bool>()), Times.Never);
    }
}
