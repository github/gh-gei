using System;
using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift;

[Collection("Environment Variables")]
public class EnvironmentVariableProviderTests
{
    private const string SOURCE_GH_PAT = "SOURCE_GH_PAT";
    private const string TARGET_GH_PAT = "TARGET_GH_PAT";

    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public EnvironmentVariableProviderTests()
    {
        _environmentVariableProvider = new EnvironmentVariableProvider(TestHelpers.CreateMock<OctoLogger>().Object);
    }

    [Fact]
    public void SourceGithubPersonalAccessToken_Should_Return_Github_Source_Pat()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GH_SOURCE_PAT", SOURCE_GH_PAT);
        Environment.SetEnvironmentVariable("GH_PAT", null);

        // Act
        var result = _environmentVariableProvider.SourceGithubPersonalAccessToken();

        // Assert
        result.Should().Be(SOURCE_GH_PAT);
    }

    [Fact]
    public void SourceGithubPersonalAccessToken_Throws_If_Github_Source_And_Target_Pats_Are_Not_Set()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GH_SOURCE_PAT", null);
        Environment.SetEnvironmentVariable("GH_PAT", null);

        // Act, Assert
        _environmentVariableProvider.Invoking(env => env.SourceGithubPersonalAccessToken())
            .Should().Throw<OctoshiftCliException>();
    }

    [Fact]
    public void SourceGithubPersonalAccessToken_Falls_Back_To_Github_Target_Pat_If_Github_Source_Pat_Is_Not_Set()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GH_SOURCE_PAT", null);
        Environment.SetEnvironmentVariable("GH_PAT", TARGET_GH_PAT);

        // Act
        var result = _environmentVariableProvider.SourceGithubPersonalAccessToken();

        // Assert
        Environment.GetEnvironmentVariable("GH_SOURCE_PAT").Should().BeNull();
        result.Should().Be(TARGET_GH_PAT);
    }

    [Fact]
    public void TargetGithubPersonalAccessToken_Should_Return_Github_Target_Pat()
    {
        // Arrange
        Environment.SetEnvironmentVariable("GH_SOURCE_PAT", null);
        Environment.SetEnvironmentVariable("GH_PAT", TARGET_GH_PAT);

        // Act
        var result = _environmentVariableProvider.TargetGithubPersonalAccessToken();

        // Assert
        result.Should().Be(TARGET_GH_PAT);
    }

    [Fact]
    public void TargetGithubPersonalAccessToken_Throws_If_Github_Source_Pat_Is_Not_Set()
    {
        Environment.SetEnvironmentVariable("GH_SOURCE_PAT", SOURCE_GH_PAT);
        Environment.SetEnvironmentVariable("GH_PAT", null);

        // Act, Assert
        _environmentVariableProvider.Invoking(env => env.TargetGithubPersonalAccessToken())
            .Should().Throw<OctoshiftCliException>();
    }
}
