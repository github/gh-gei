using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
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
            environmentVariableProviderMock.Setup(m => m.GithubPersonalAccessToken()).Returns(GH_PAT);

            using var httpClient = new HttpClient();

            // Act
            var factory = new GithubApiFactory(null, httpClient, environmentVariableProviderMock.Object, null, null);
            var result = factory.Create();

            // Assert
            result.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.GithubPersonalAccessToken());
        }

        [Fact]
        public void Create_Should_Create_Github_Api_With_Provided_Github_Pat()
        {
            // Arrange
            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock.Setup(m => m.GithubPersonalAccessToken()).Returns(GH_PAT);

            using var httpClient = new HttpClient();

            // Act
            var factory = new GithubApiFactory(null, httpClient, environmentVariableProviderMock.Object, null, null);
            var result = factory.Create(personalAccessToken: GH_PAT);

            // Assert
            result.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.GithubPersonalAccessToken(), Times.Never);
        }
    }
}
