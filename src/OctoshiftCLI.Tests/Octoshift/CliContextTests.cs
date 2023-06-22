using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift;

public class CliContextTests
{
    [Fact]
    public void Set_RootCommand_Should_Remove_Gh_Prefix()
    {
        CliContext.Clear();

        CliContext.RootCommand = "gh-gei";
        CliContext.RootCommand.Should().Be("gei");

        CliContext.Clear();
    }

    [Fact]
    public void Set_RootCommand_Should_Not_Change_Value_Without_Gh_Prefix()
    {
        CliContext.Clear();

        CliContext.RootCommand = "bbs2gh";
        CliContext.RootCommand.Should().Be("bbs2gh");

        CliContext.Clear();
    }
}
