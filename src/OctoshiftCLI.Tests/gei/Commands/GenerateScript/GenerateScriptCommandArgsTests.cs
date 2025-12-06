using System.IO;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommandArgsTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private const string SOURCE_ORG = "FOO-SOURCE-ORG";
        private const string TARGET_ORG = "FOO-TARGET-ORG";
        private const string AWS_BUCKET_NAME = "AWS_BUCKET_NAME";

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Is_Provided_But_Ghes_Api_Url_Is_Not()
        {
            // Arrange
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                AwsBucketName = AWS_BUCKET_NAME,
                Sequential = true
            };

            // Act, Assert
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .Throw<OctoshiftCliException>();
        }

        [Fact]
        public void It_Throws_When_No_Ssl_Verify_Is_Set_But_Ghes_Api_Url_Is_Not()
        {
            // Arrange
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                NoSslVerify = true,
                Sequential = true
            };

            // Act, Assert
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .Throw<OctoshiftCliException>();
        }

        [Fact]
        public void It_Throws_When_Invalid_URL_Provided()
        {
            // Arrange
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                NoSslVerify = true,
                Sequential = true,
                GhesApiUrl = "ww.invalidUr/l.c"
            };

            // Act, Assert
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .Throw<OctoshiftCliException>();
        }

        [Fact]
        public void UseGithubStorage_Without_Ghes_Api_Url_Throws()
        {
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = "foo",
                GithubTargetOrg = "bar",
                UseGithubStorage = true
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                 .Should()
                 .ThrowExactly<OctoshiftCliException>()
                 .WithMessage("*--use-github-storage*");
        }


        [Fact]
        public void UseGithubStorage_And_Aws_Bucket_Name_Throws()
        {
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = "foo",
                GithubTargetOrg = "bar",
                AwsBucketName = "aws",
                GhesApiUrl = "https://github.contoso.com",
                UseGithubStorage = true
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                 .Should()
                 .ThrowExactly<OctoshiftCliException>()
                 .WithMessage("*--use-github-storage flag was provided with an AWS S3 Bucket name*");
        }

        [Fact]
        public void Validate_Throws_When_GithubSourceOrg_Is_Url()
        {
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = "https://github.com/my-org",
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("The --github-source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        [Fact]
        public void Validate_Throws_When_GithubTargetOrg_Is_Url()
        {
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = "https://github.com/my-org",
                Output = new FileInfo("unit-test-output")
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("The --github-target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }
    }
}
