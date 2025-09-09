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
        private const string GIT_ARCHIVE_URL = "http://host/git-archive.tar.gz";
        private const string METADATA_ARCHIVE_URL = "http://host/metadata-archive.tar.gz";
        private const string GIT_ARCHIVE_PATH = "./git-archive.tar.gz";
        private const string METADATA_ARCHIVE_PATH = "./metadata-archive.tar.gz";

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
        public void AwsBucketName_Validates_With_GhesApiUrl()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                AwsBucketName = AWS_BUCKET_NAME,
                GhesApiUrl = GHES_API_URL
            };

            args.Validate(_mockOctoLogger.Object);

            args.TargetRepo.Should().Be(SOURCE_REPO);
        }

        [Fact]
        public void AwsBucketName_Validates_With_ArchivePaths()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                AwsBucketName = AWS_BUCKET_NAME,
                GitArchivePath = GIT_ARCHIVE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };

            args.Validate(_mockOctoLogger.Object);

            args.TargetRepo.Should().Be(SOURCE_REPO);
        }

        [Fact]
        public void UseGithubStorage_Validates_With_GhesApiUrl()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                GhesApiUrl = GHES_API_URL,
                UseGithubStorage = true
            };

            args.Validate(_mockOctoLogger.Object);

            args.TargetRepo.Should().Be(SOURCE_REPO);
        }

        [Fact]
        public void UseGithubStorage_Validates_With_ArchivePaths()
        {
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                GitArchivePath = GIT_ARCHIVE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_PATH,
                UseGithubStorage = true
            };

            args.Validate(_mockOctoLogger.Object);

            args.TargetRepo.Should().Be(SOURCE_REPO);
        }

        [Fact]
        public void Aws_Bucket_Name_Without_GhesApiUrl_Or_ArchivePaths_Throws()
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
        public void UseGithubStorage_Without_GhesApiUrl_Or_ArchivePaths_Throws()
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
                .WithMessage("*--use-github-storage flag*");
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

        [Fact]
        public void GitArchivePath_Without_MetadataArchivePath_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_PATH
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*you must provide both --git-archive-path --metadata-archive-path*");
        }

        [Fact]
        public void MetadataArchivePath_Without_GitArchivePath_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*you must provide both --git-archive-path --metadata-archive-path*");
        }

        [Fact]
        public void GitArchiveUrl_With_GitArchivePath_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchiveUrl = GIT_ARCHIVE_URL,
                GitArchivePath = GIT_ARCHIVE_PATH
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--git-archive-url and --git-archive-path may not be used together*");
        }

        [Fact]
        public void MetadataArchiveUrl_With_MetadataArchivePath_Throws()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                MetadataArchiveUrl = METADATA_ARCHIVE_URL,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--metadata-archive-url and --metadata-archive-path may not be used together*");
        }
        [Fact]
        public void It_Throws_Error_When_Url_Provided_In_GithubSourceOrg()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = "https://github.com/foo",
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                MetadataArchiveUrl = METADATA_ARCHIVE_URL,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*GithubSourceOrg should be an org name, not a URL*");
        }
        [Fact]
        public void It_Throws_Error_When_Url_Provided_In_GithubTargetOrg()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = "https://github.com/bar",
                TargetRepo = TARGET_REPO,
                MetadataArchiveUrl = METADATA_ARCHIVE_URL,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*GithubTargetOrg should be an org name, not a URL*");
        }
        [Fact]
        public void It_Throws_Error_When_Url_Provided_In_Both_Source_And_Target_Org()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = "https://github.com/foo",
                GithubTargetOrg = "https://github.com/bar",
                TargetRepo = TARGET_REPO,
                MetadataArchiveUrl = METADATA_ARCHIVE_URL,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*GithubSourceOrg should be an org name, not a URL*"); 
        }
        [Fact]
        public void It_Throws_Error_When_Url_Provided_In_SourceRepo()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = "https://github.com/foo",
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                MetadataArchiveUrl = METADATA_ARCHIVE_URL,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*SourceRepo should be a repo name, not a URL*");
        }
        [Fact]
        public void It_Throws_Error_When_Url_Provided_In_TargetRepo()
        {
            var args = new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = "https://github.com/bar",
                MetadataArchiveUrl = METADATA_ARCHIVE_URL,
                MetadataArchivePath = METADATA_ARCHIVE_PATH
            };  
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*TargetRepo should be a repo name, not a URL*");
        }
    }
}
