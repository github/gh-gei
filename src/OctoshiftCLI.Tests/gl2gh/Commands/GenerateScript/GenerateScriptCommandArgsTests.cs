using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub.Commands.GenerateScript;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.GenerateScript;

public class GenerateScriptCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly GenerateScriptCommandArgs _args = new();

    [Fact]
    public void It_Throws_If_Both_AwsBucketName_And_UseGithubStorage_Are_Provided()
    {
        // Arrange
        _args.AwsBucketName = "my-bucket";
        _args.UseGithubStorage = true;

        // Act & Assert
        _args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.");
    }

    [Fact]
    public void It_Throws_If_Both_AwsRegion_And_UseGithubStorage_Are_Provided()
    {
        // Arrange
        _args.AwsRegion = "aws-region";
        _args.UseGithubStorage = true;

        // Act & Assert
        _args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --use-github-storage flag was provided with an AWS S3 region. Archive cannot be uploaded to both locations.");
    }
}
