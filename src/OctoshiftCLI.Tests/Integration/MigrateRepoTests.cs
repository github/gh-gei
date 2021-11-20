using Xunit;

namespace OctoshiftCLI.Tests.Integration;

public class MigrateRepoTests
{
    [Fact]
    public async Task ShouldMirateWithEmptyRepo()
    {
        // Arrange
        var targetRepo = TestHelpers.GetTargetName("empty-repo");

        //TODO: Perform the migration (uncomment below) then reverse polarity of the test
        // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)

        // var parameterString = $"migrate-repo --ado-org {TestHelpers.SourceOrg} --ado-team-project \"int-git\" --ado-repo \"git-empty\" --github-org {TestHelpers.TargetOrg} --github-repo {targetRepo}";
        // var parameters = parameterString.Trim().Split(' ');
        // await OctoshiftCLI.Program.Main(parameters);

        // Act
        var exists = await TestHelpers.RepoExists(TestHelpers.TargetOrg, targetRepo);

        // Assert
        Assert.False(exists);

        // Cleanup
        await TestHelpers.DeleteRepo(TestHelpers.TargetOrg, targetRepo);
    }

    [Fact]
    public async Task ShouldIncludeHistoryWithPopulatedRepo()
    {
        // Arrange
        var targetRepo = TestHelpers.GetTargetName("repo");

        //TODO: Perform the migration (uncomment below) then reverse polarity of the test
        // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)

        // var parameterString = $"migrate-repo --ado-org {TestHelpers.SourceOrg} --ado-team-project \"int-git\" --ado-repo \"int-git1\" --github-org {TestHelpers.TargetOrg} --github-repo {targetRepo}";
        // var parameters = parameterString.Trim().Split(' ');
        // await OctoshiftCLI.Program.Main(parameters);

        // Act
        var exists = await TestHelpers.RepoExists(TestHelpers.TargetOrg, "int-git1");

        // Assert
        Assert.False(exists);

        //TODO: Add verification of file history

        // Cleanup
        await TestHelpers.DeleteRepo(TestHelpers.TargetOrg, targetRepo);
    }
}