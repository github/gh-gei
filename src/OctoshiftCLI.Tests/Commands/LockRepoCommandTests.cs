using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands;

public class LockRepoCommandTests
{
    [Fact]
    public void ShouldHaveOptions()
    {
        var command = new LockRepoCommand(null, null);
        Assert.NotNull(command);
        Assert.Equal("lock-ado-repo", command.Name);
        Assert.Equal(4, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
        TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }

    [Fact]
    public async Task HappyPath()
    {
        var adoOrg = "FooOrg";
        var adoTeamProject = "BlahTeamProject";
        var adoRepo = "foo-repo";
        var repoId = Guid.NewGuid().ToString();
        var identityDescriptor = "foo-id";
        var teamProjectId = Guid.NewGuid().ToString();

        var mockAdo = new Mock<AdoApi>(null);
        mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
        mockAdo.Setup(x => x.GetRepoId(adoOrg, adoTeamProject, adoRepo).Result).Returns(repoId);
        mockAdo.Setup(x => x.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users").Result).Returns(identityDescriptor);

        using var adoFactory = new AdoApiFactory(mockAdo.Object);

        var command = new LockRepoCommand(new Mock<OctoLogger>().Object, adoFactory);
        await command.Invoke(adoOrg, adoTeamProject, adoRepo);

        mockAdo.Verify(x => x.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor));
    }
}