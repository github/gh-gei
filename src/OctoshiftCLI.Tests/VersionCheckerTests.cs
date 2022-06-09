using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class VersionCheckerTests
    {
        [Fact]
        public void GetVersionComments_Returns_Root_And_Executing_Commands()
        {
            // Arrange
            const string rootCommand = "ROOT_COMMAND";
            const string executingCommand = "EXECUTING_COMMAND";

            CliContext.RootCommand = rootCommand;
            CliContext.ExecutingCommand = executingCommand;

            var versionChecker = new VersionChecker(null, null);

            // Act
            var comments = versionChecker.GetVersionComments();

            // Assert
            comments.Should().Be($"({rootCommand}/{executingCommand})");
        }
    }
}
