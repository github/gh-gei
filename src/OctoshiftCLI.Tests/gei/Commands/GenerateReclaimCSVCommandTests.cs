using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GenerateReclaimCsvCommandTests
    {
        private const string TARGET_API_URL = "https://api.github.com";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateReclaimCsvCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("generate-reclaim-csv", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "output", true);
            TestHelpers.VerifyCommandOption(command.Options, "include-reclaimed", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async void NoMannequins_GenerateEmptyCSV_WithOnlyHeaders()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(Array.Empty<Mannequin>());

            string csvContent = null;

            var command = new GenerateReclaimCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockTargetGithubApiFactory.Object,
                TestHelpers.CreateMock<EnvironmentVariableProvider>().Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            await command.Invoke("octocat", "unit-test-output", false);

            // Assert
            csvContent.Should().Be("login,claimantlogin\n");
        }

        [Fact]
        public async void Mannequins_GenerateCSV_UnreclaimedOnly()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();

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


            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);

            string csvContent = null;

            var command = new GenerateReclaimCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockTargetGithubApiFactory.Object,
                TestHelpers.CreateMock<EnvironmentVariableProvider>().Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            await command.Invoke(githubOrg, "unit-test-output", false);

            // Assert
            csvContent.Should().Be("login,claimantlogin\nmona,\n");
            mockGithubApi.Verify(x => x.GetMannequins(githubOrgId));
        }

        [Fact]
        public async void Mannequins_GenerateCSV_IncludeAlreadyReclaimed()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();

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


            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);

            string csvContent = null;

            var command = new GenerateReclaimCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockTargetGithubApiFactory.Object,
                TestHelpers.CreateMock<EnvironmentVariableProvider>().Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            await command.Invoke(githubOrg, "unit-test-output", true);

            // Assert
            csvContent.Should().Be("login,claimantlogin\nmona,\nmonalisa,monalisa_gh\n");
            mockGithubApi.Verify(x => x.GetMannequins(githubOrgId));
        }
    }
}
