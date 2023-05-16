using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.MigrateRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string ADO_ORG = "FooOrg";
    private const string ADO_REPO = "foo-repo";
    private const string GITHUB_ORG = "foo-gh-org";
    private const string GITHUB_REPO = "gh-repo";

    [Fact]
    public void Validates_Wait_And_QueueOnly_Not_Passed_Together()
    {
        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Wait = true,
            QueueOnly = true,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                           .Should()
                           .ThrowExactly<OctoshiftCliException>()
                           .WithMessage("*wait*");
    }

    [Fact]
    public void Wait_Flag_Shows_Warning()
    {
        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Wait = true,
        };

        args.Validate(_mockOctoLogger.Object);

        _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(x => x.ToLower().Contains("wait"))));
    }

    [Fact]
    public void No_Wait_And_No_Queue_Only_Flags_Shows_Warning()
    {
        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
        };

        args.Validate(_mockOctoLogger.Object);

        _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(x => x.ToLower().Contains("wait"))));
    }
}
