using FluentAssertions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class VersionCheckerTests
{
    [Fact]
    public void GetVersionComments_Returns_Root_And_Executing_Commands()
    {
        // Arrange
        TestHelpers.SetCliContext();

        var versionChecker = new VersionChecker(null, null);

        // Act
        var comments = versionChecker.GetVersionComments();

        // Assert
        comments.Should().Be($"({TestHelpers.CLI_ROOT_COMMAND}/{TestHelpers.CLI_EXECUTING_COMMAND})");
    }
}
