using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class EnvironmentVariableProviderTests
    {
        private const string ADO_PAT = "ADO_PAT";
        private const string GH_PAT = "GH_PAT";

        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly Mock<OctoLogger> _mockLogger;

        public EnvironmentVariableProviderTests()
        {
            _mockLogger = new Mock<OctoLogger>();
            _environmentVariableProvider = new EnvironmentVariableProvider(_mockLogger.Object);
        }

        [Fact]
        public void GithubPersonalAccessToken_Should_Return_Github_Pat()
        {
            // Arrange 
            ResetEnvs(GH_PAT);

            // Act
            var result = _environmentVariableProvider.GithubPersonalAccessToken();

            // Assert
            result.Should().Be(GH_PAT);
        }

        [Fact]
        public void GithubPersonalAccessToken_Registers_Github_Pat_Against_Logger()
        {
            // Arrange
            ResetEnvs(GH_PAT);

            // Act
            var result = _environmentVariableProvider.GithubPersonalAccessToken();

            // Assert
            _mockLogger.Verify(m => m.RegisterSecret(result));
        }

        [Fact]
        public void GithubPersonalAccessToken_Throws_If_Github_Pat_Is_Not_Set()
        {
            // Arrange
            ResetEnvs();

            // Act, Assert
            _environmentVariableProvider.Invoking(env => env.GithubPersonalAccessToken())
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AdoPersonalAccessToken_Should_Return_Ado_Pat()
        {
            // Arrange
            ResetEnvs(adoPat: ADO_PAT);

            // Act
            var result = _environmentVariableProvider.AdoPersonalAccessToken();

            // Assert
            result.Should().Be(ADO_PAT);
        }

        [Fact]
        public void AdoPersonalAccessToken_Registers_Ado_Pat_Against_Logger()
        {
            // Arrange
            ResetEnvs(adoPat: ADO_PAT);

            // Act
            var result = _environmentVariableProvider.AdoPersonalAccessToken();

            // Assert
            _mockLogger.Verify(m => m.RegisterSecret(result));
        }

        [Fact]
        public void AdoPersonalAccessToken_Throws_If_Ado_Pat_Is_Not_Set()
        {
            // Arrange
            ResetEnvs();

            // Act, Assert
            _environmentVariableProvider.Invoking(env => env.AdoPersonalAccessToken())
                .Should().Throw<ArgumentNullException>();
        }

        private void ResetEnvs(string githubPat = null, string adoPat = null)
        {
            Environment.SetEnvironmentVariable("GH_PAT", githubPat);
            Environment.SetEnvironmentVariable("ADO_PAT", adoPat);
        }
    }
}