using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

[Collection("Environment Variables")]
public class EnvironmentVariableProviderTests
{
    private const string SOURCE_GH_PAT = "GH_SOURCE_PAT";
    private const string TARGET_GH_PAT = "GH_PAT";
    private const string ADO_PAT = "ADO_PAT";
    private const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";
    private const string AWS_ACCESS_KEY_ID = "AWS_ACCESS_KEY_ID";
    private const string AWS_SECRET_ACCESS_KEY = "AWS_SECRET_ACCESS_KEY";
    private const string BBS_USERNAME = "BBS_USERNAME";
    private const string BBS_PASSWORD = "BBS_PASSWORD";
    private const string GEI_SKIP_STATUS_CHECK = "GEI_SKIP_STATUS_CHECK";
    private const string GEI_SKIP_VERSION_CHECK = "GEI_SKIP_VERSION_CHECK";

    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly Mock<OctoLogger> _mockLogger = TestHelpers.CreateMock<OctoLogger>();

    public EnvironmentVariableProviderTests()
    {
        _environmentVariableProvider = new EnvironmentVariableProvider(_mockLogger.Object);
        ResetEnvs();
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
    public void TargetGithubPersonalAccessToken_Throws_If_Github_Target_Pat_Is_Not_Set()
    {
        Environment.SetEnvironmentVariable("GH_SOURCE_PAT", SOURCE_GH_PAT);
        Environment.SetEnvironmentVariable("GH_PAT", null);

        // Act, Assert
        _environmentVariableProvider.Invoking(env => env.TargetGithubPersonalAccessToken())
            .Should().Throw<OctoshiftCliException>();
    }

    [Fact]
    public void TargetGithubPersonalAccessToken_Registers_Github_Pat_Against_Logger()
    {
        // Arrange
        var secretValue = "foo";
        Environment.SetEnvironmentVariable(TARGET_GH_PAT, secretValue);

        // Act
        _environmentVariableProvider.TargetGithubPersonalAccessToken();

        // Assert
        _mockLogger.Verify(m => m.RegisterSecret(secretValue));
    }

    [Fact]
    public void TargetGithubPersonalAccessToken_Throws_If_Github_Pat_Is_Not_Set()
    {
        _environmentVariableProvider.Invoking(env => env.TargetGithubPersonalAccessToken())
            .Should().Throw<OctoshiftCliException>();
    }

    [Fact]
    public void SkipStatusCheck_WhitespacesOnly_Should_Return_Null()
    {
        // Arrange
        Environment.SetEnvironmentVariable(GEI_SKIP_STATUS_CHECK, " ");

        // Act
        var result = _environmentVariableProvider.SkipStatusCheck();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SkipStatusCheck_DoesNot_RegisterValue_Against_Logger()
    {
        // Arrange
        var expectedValue = "true";
        Environment.SetEnvironmentVariable(GEI_SKIP_STATUS_CHECK, expectedValue);

        // Act
        var value = _environmentVariableProvider.SkipStatusCheck();

        // Assert
        value.Should().Be(expectedValue);
        _mockLogger.VerifyNoOtherCalls();
    }

    private void ResetEnvs()
    {
        Environment.SetEnvironmentVariable(SOURCE_GH_PAT, null);
        Environment.SetEnvironmentVariable(TARGET_GH_PAT, null);
        Environment.SetEnvironmentVariable(ADO_PAT, null);
        Environment.SetEnvironmentVariable(AZURE_STORAGE_CONNECTION_STRING, null);
        Environment.SetEnvironmentVariable(AWS_ACCESS_KEY_ID, null);
        Environment.SetEnvironmentVariable(AWS_SECRET_ACCESS_KEY, null);
        Environment.SetEnvironmentVariable(BBS_USERNAME, null);
        Environment.SetEnvironmentVariable(BBS_PASSWORD, null);
        Environment.SetEnvironmentVariable(GEI_SKIP_STATUS_CHECK, null);
        Environment.SetEnvironmentVariable(GEI_SKIP_VERSION_CHECK, null);
    }
}
