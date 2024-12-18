using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands.MigrateRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgsTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private const string ARCHIVE_PATH = "path/to/archive.tar";
        private const string ARCHIVE_URL = "https://archive-url/bbs-archive.tar";
        private const string GITHUB_ORG = "target-org";
        private const string GITHUB_REPO = "target-repo";
        private const string GITHUB_PAT = "github pat";
        private const string AWS_ACCESS_KEY_ID = "aws-access-key-id";
        private const string AWS_SECRET_ACCESS_KEY = "aws-secret-access-key";
        private const string AWS_SESSION_TOKEN = "aws-session-token";
        private const string AWS_REGION = "aws-region";
        private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";
        private const string AWS_BUCKET_NAME = "aws-bucket-name";
        private const string BBS_HOST = "our-bbs-server.com";
        private const string BBS_SERVER_URL = $"https://{BBS_HOST}";
        private const string BBS_USERNAME = "bbs-username";
        private const string BBS_PASSWORD = "bbs-password";
        private const string BBS_PROJECT = "bbs-project";
        private const string BBS_REPO = "bbs-repo";
        private const string SSH_USER = "ssh-user";
        private const string PRIVATE_KEY = "private-key";
        private const string SMB_USER = "smb-user";
        private const string SMB_PASSWORD = "smb-password";

        [Fact]
        public void It_Throws_When_Kerberos_Is_Set_And_Bbs_Password_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                BbsPassword = BBS_PASSWORD,
                Kerberos = true
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-password*--kerberos*");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Access_Key_Provided()
        {
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Provided_With_UseGithubStorage_Option()
        {
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsBucketName = AWS_BUCKET_NAME,
                UseGithubStorage = true
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--use-github-storage flag was provided with an AWS S3 Bucket name*");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Provided_With_AzureStorageConnectionString_Option()
        {
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsBucketName = AWS_BUCKET_NAME,
                UseGithubStorage = true
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*Archive cannot be uploaded to both locations.");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Secret_Key_Provided()
        {
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Session_Token_Provided()
        {
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsSessionToken = AWS_SESSION_TOKEN
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public void It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Region_Provided()
        {
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsRegion = AWS_REGION
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public void Errors_If_BbsServer_Url_Provided_But_No_Bbs_Project()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsRepo = BBS_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-project*");
        }

        [Fact]
        public void Errors_If_BbsServer_Url_Provided_But_No_Bbs_Repo()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsProject = BBS_PROJECT,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-repo*");
        }

        [Fact]
        public void It_Throws_When_Kerberos_Is_Set_And_Bbs_Username_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                BbsUsername = BBS_USERNAME,
                Kerberos = true
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-username*--kerberos*");
        }

        [Fact]
        public void Errors_If_Bbs_Password_Is_Provided_With_Archive_Path()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsPassword = BBS_USERNAME
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-username*--bbs-password*--archive-path*");
        }

        [Fact]
        public void Errors_If_Bbs_Password_Is_Provided_With_Archive_Url()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsPassword = BBS_USERNAME
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-username*--bbs-password*--archive-url*");
        }

        [Fact]
        public void Errors_If_No_Ssl_Verify_Is_Provided_With_Archive_Path()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                NoSslVerify = true
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--no-ssl-verify*--archive-path*");
        }

        [Fact]
        public void Errors_If_No_Ssl_Verify_Is_Provided_With_Archive_Url()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                NoSslVerify = true
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--no-ssl-verify*--archive-url*");
        }

        [Fact]
        public void Errors_If_Ssh_User_Is_Provided_With_Archive_Path()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*SSH*SMB*--archive-path*");
        }

        [Fact]
        public void Errors_If_Ssh_User_Is_Provided_With_Archive_Url()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*SSH*SMB*--archive-url*");
        }

        [Fact]
        public void Errors_If_Smb_User_Is_Provided_With_Archive_Path()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SmbUser = SMB_USER,
                SmbPassword = SMB_PASSWORD,
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*SSH*SMB*--archive-path*");
        }

        [Fact]
        public void Errors_If_Smb_User_Is_Provided_With_Archive_Url()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SmbUser = SMB_USER,
                SmbPassword = SMB_PASSWORD,
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*SSH*SMB*--archive-url*");
        }

        [Fact]
        public void It_Throws_If_Github_Org_Is_Provided_But_Github_Repo_Is_Not()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                BbsServerUrl = BBS_SERVER_URL,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                GithubPat = GITHUB_PAT,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--github-repo*");
        }

        [Fact]
        public void It_Throws_If_Archive_Url_Is_Provided_But_Github_Org_Is_Not()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubPat = GITHUB_PAT,
                ArchiveUrl = ARCHIVE_URL,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--github-org*");
        }

        [Fact]
        public void It_Throws_If_Archive_Url_Is_Provided_But_Github_Repo_Is_Not()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubPat = GITHUB_PAT,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--github-repo*");
        }

        [Fact]
        public void Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_User_And_Smb_User_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SmbUser = SMB_USER
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().ThrowExactly<OctoshiftCliException>();
        }

        [Fact]
        public void Errors_When_Archive_Download_Host_Provided_Without_Ssh_Or_Smb_Options()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                ArchiveDownloadHost = "somehost"
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().ThrowExactly<OctoshiftCliException>();
        }

        [Fact]
        public void Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_User_And_Smb_Password_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SmbPassword = SMB_PASSWORD
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().ThrowExactly<OctoshiftCliException>();
        }

        [Fact]
        public void Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_Private_Key_And_Smb_User_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshPrivateKey = PRIVATE_KEY,
                SmbUser = SMB_USER
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().ThrowExactly<OctoshiftCliException>();
        }

        [Fact]
        public void Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_Private_Key_And_Smb_Password_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                SmbPassword = SMB_PASSWORD
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().ThrowExactly<OctoshiftCliException>();
        }

        [Fact]
        public void Invoke_With_Bbs_Server_Url_Throws_When_Ssh_User_Is_Provided_And_Ssh_Private_Key_Is_Not_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER
            };

            args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().ThrowExactly<OctoshiftCliException>();
        }

        [Fact]
        public void Errors_If_Archive_Url_And_Archive_Path_Are_Passed()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--archive-path*--archive-url*");
        }

        [Fact]
        public void Allows_BbsServer_Url_And_Archive_Url_To_Be_Passed_Together()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Allows_BbsServer_Url_And_Archive_Path_To_Be_Passed_Together()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .NotThrow();
        }

        [Fact]
        public void Errors_If_BbsServer_Url_Archive_Path_And_Archive_Url_Are_Not_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            args.Invoking(x => x.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*--bbs-server-url*--archive-path*--archive-url*");
        }

        [Fact]
        public void Invoke_With_Ssh_Port_Set_To_7999_Logs_Warning()
        {
            var args = new MigrateRepoCommandArgs
            {
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SshPort = 7999
            };

            args.Validate(_mockOctoLogger.Object);

            _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(x => x.ToLower().Contains("--ssh-port is set to 7999"))));
        }
    }
}
