using System;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class AdoApiFactoryTests
    {
        private const string ADO_PAT = "ADO_PAT";

        [Fact]
        public void Create_Should_Create_Ado_Api_With_Ado_Pat()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken()).Returns(ADO_PAT);

            using var httpClient = new HttpClient();

            // Act
            var factory = new AdoApiFactory(null, httpClient, environmentVariableProviderMock.Object);
            var result = factory.Create();

            // Assert
            result.Should().BeOfType<AdoApi>();

            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{ADO_PAT}"));
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(authToken);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Basic");
        }
    }
}