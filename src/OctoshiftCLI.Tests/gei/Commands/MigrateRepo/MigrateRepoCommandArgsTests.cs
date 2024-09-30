using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgsTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private const string SOURCE_ORG = "foo-source-org";
        private const string SOURCE_REPO = "foo-repo-source";
        private const string TARGET_ORG = "foo-target-org";
        private const string TARGET_REPO = "foo-target-repo";
        private const string GITHUB_TARGET_PAT = "github-target-pat";
        private const string AWS_BUCKET_NAME = "aws-bucket-name";
        private const string GHES_API_URL = "foo-ghes-api.com";

        [Fact]
        public void Defaults_TargetRepo_To_SourceRepo()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
            };

            args.Validate(_mockOctoLogger.Object);

            args.TargetRepo.Should().Be(SOURCE_REPO);
        }

        [Fact]
        public void It_Falls_Back_To_Github_Target_Pat_If_Github_Source_Pat_Is_Not_Provided()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GithubTargetPat = GITHUB_TARGET_PAT,
                QueueOnly = true,
            };

            args.Validate(_mockOctoLogger.Object);

            args.GithubSourcePat.Should().Be(GITHUB_TARGET_PAT);
        }

        [Fact]
        public void Aws_Bucket_Name_Without_Ghes_Api_Url_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                AwsBucketName = AWS_BUCKET_NAME
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                 .Should()
                 .ThrowExactly<OctoshiftCliException>()
                 .WithMessage("*--aws-bucket-name*");
        }

        [Fact]
        public void UseGithubStorage_Without_Ghes_Api_Url_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
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
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                AwsBucketName = AWS_BUCKET_NAME,
                GhesApiUrl = GHES_API_URL,
                UseGithubStorage = true
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                 .Should()
                 .ThrowExactly<OctoshiftCliException>()
                 .WithMessage("*--use-github-storage flag was provided with an AWS S3 Bucket name*");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Provided_With_AzureStorageConnectionString_Option()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                AwsBucketName = AWS_BUCKET_NAME,
                GhesApiUrl = GHES_API_URL,
                UseGithubStorage = true
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--use-github-storage flag was provided with a connection string for an Azure storage account*");
        }
        [Fact]
        public void No_Ssl_Verify_Without_Ghes_Api_Url_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                NoSslVerify = true
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--no-ssl-verify*");
        }

        [Fact]
        public void Keep_Archive_Without_Ghes_Api_Url_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                KeepArchive = true
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--keep-archive*");
        }
    }
}
