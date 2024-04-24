using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.GrantMigratorRole;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.GrantMigratorRole;

public class GrantMigratorRoleCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string GITHUB_ORG = "FooOrg";
    private const string ACTOR = "foo-actor";

    [Fact]
    public void Invalid_Actor_Type()
    {
        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = "INVALID",
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should().Throw<OctoshiftCliException>();
    }

    [Fact]
    public void It_Validates_GhesApiUrl_And_TargetApiUrl()
    {
        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = "USER",
            GhesApiUrl = "https://ghes.example.com",
            TargetApiUrl = "https://api.github.com",
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should().Throw<OctoshiftCliException>();
    }
}
