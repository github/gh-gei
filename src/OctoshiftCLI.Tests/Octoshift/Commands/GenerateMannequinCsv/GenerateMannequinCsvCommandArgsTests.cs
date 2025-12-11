using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.GenerateMannequinCsv;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.GenerateMannequinCsv;

public class GenerateMannequinCsvCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string GITHUB_ORG = "foo-org";

    [Fact]
    public void Validate_Throws_When_GithubOrg_Is_Url()
    {
        var args = new GenerateMannequinCsvCommandArgs
        {
            GithubOrg = "http://github.com/my-org"
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
    }

    [Fact]
    public void Validate_Succeeds_With_Valid_Name()
    {
        var args = new GenerateMannequinCsvCommandArgs
        {
            GithubOrg = GITHUB_ORG
        };

        args.Validate(_mockOctoLogger.Object);

        args.GithubOrg.Should().Be(GITHUB_ORG);
    }
}
