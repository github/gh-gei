using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GenerateMannequinCsvCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly GenerateMannequinCsvCommand _command;

        private const string CSV_HEADER = "mannequin-user,mannequin-id,target-user";
        private const string GITHUB_ORG = "FooOrg";
        private readonly string GITHUB_ORG_ID = Guid.NewGuid().ToString();
        private string _csvContent = string.Empty;

        public GenerateMannequinCsvCommandTests()
        {
            _command = new GenerateMannequinCsvCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    _csvContent = contents;
                    return Task.CompletedTask;
                }
            };
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("generate-mannequin-csv", _command.Name);
            Assert.Equal(6, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "output", false);
            TestHelpers.VerifyCommandOption(_command.Options, "include-reclaimed", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task NoMannequins_GenerateEmptyCSV_WithOnlyHeaders()
        {
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.GetMannequins(GITHUB_ORG_ID).Result).Returns(Array.Empty<Mannequin>());

            var expected = CSV_HEADER + Environment.NewLine;

            // Act
            await _command.Invoke("octocat", new FileInfo("unit-test-output"), false);

            // Assert
            _csvContent.Should().Be(expected);
        }

        [Fact]
        public async Task Mannequins_GenerateCSV_UnreclaimedOnly()
        {
            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = "monaid",
                    Login = "mona"
                },
                new Mannequin
                {
                    Id = "monalisaid",
                    Login = "monalisa",
                    MappedUser = new Claimant
                    {
                        Id = "monalisamapped-id",
                        Login = "monalisa_gh"
                    }
                }
            };

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.GetMannequins(GITHUB_ORG_ID).Result).Returns(mannequinsResponse);

            var expected = CSV_HEADER + Environment.NewLine
                + "mona,monaid," + Environment.NewLine;

            // Act
            await _command.Invoke(GITHUB_ORG, new FileInfo("unit-test-output"), false);

            // Assert
            _csvContent.Should().Be(expected);
        }

        [Fact]
        public async Task Mannequins_GenerateCSV_IncludeAlreadyReclaimed()
        {
            var mannequinsResponse = new[]
            {
                new Mannequin
                {
                    Id = "monaid",
                    Login = "mona"
                },
                new Mannequin
                {
                    Id = "monalisaid",
                    Login = "monalisa",
                    MappedUser = new Claimant
                    {
                        Id = "monalisamapped-id",
                        Login = "monalisa_gh"
                    }
                }
            };

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.GetMannequins(GITHUB_ORG_ID).Result).Returns(mannequinsResponse);

            var expected = CSV_HEADER + Environment.NewLine
                + "mona,monaid," + Environment.NewLine
                + "monalisa,monalisaid,monalisa_gh" + Environment.NewLine;

            // Act
            await _command.Invoke(GITHUB_ORG, new FileInfo("unit-test-output"), true);

            // Assert
            _csvContent.Should().Be(expected);
        }

        [Fact]
        public async Task It_Uses_Target_Api_Url_When_Provided()
        {
            // Arrange
            const string targetApiUrl = "https://api.contoso.com";

            _mockTargetGithubApiFactory.Setup(m => m.Create(targetApiUrl, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            // Act
            await _command.Invoke(GITHUB_ORG, new FileInfo("unit-test-output"), targetApiUrl: targetApiUrl);

            // Assert
            _mockOctoLogger.Verify(m => m.LogInformation($"TARGET API URL: {targetApiUrl}"));
            _mockTargetGithubApiFactory.Verify(m => m.Create(targetApiUrl, null));
        }
    }
}
