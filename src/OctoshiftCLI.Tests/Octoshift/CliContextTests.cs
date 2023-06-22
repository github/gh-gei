using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift;

public class CliContextTests
{
    [Fact]
    public void SetRootCommandShouldRemoveGhPrefix()
    {
        CliContext.Clear();

        CliContext.RootCommand = "gh-gei";
        CliContext.RootCommand.Should().Be("gei");

        CliContext.Clear();
    }

    [Fact]
    public void SetRootCommandShouldNotChangeValueWithoutGhPrefix()
    {
        CliContext.Clear();

        CliContext.RootCommand = "bbs2gh";
        CliContext.RootCommand.Should().Be("bbs2gh");

        CliContext.Clear();
    }
}
