using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GithubApiFactoryTests
    {
        private const string GH_PAT = "GH_PAT";

        [Fact]
        public void Create_Should_Create_Github_Api_With_Github_Pat_From_Environment_If_Not_Provided()
        {
            // Arrange
            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GH_PAT);

            using var httpClient = new HttpClient();

            // Act
            var factory = new GithubApiFactory(null, httpClient, environmentVariableProviderMock.Object, null, null, null);
            var result = factory.Create();

            // Assert
            result.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()));
        }

        [Fact]
        public void Create_Should_Create_Github_Api_With_Provided_Github_Pat()
        {
            // Arrange
            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GH_PAT);

            using var httpClient = new HttpClient();

            // Act
            var factory = new GithubApiFactory(null, httpClient, environmentVariableProviderMock.Object, null, null, null);
            var result = factory.Create(targetPersonalAccessToken: GH_PAT);

            // Assert
            result.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
        }
    }
}
