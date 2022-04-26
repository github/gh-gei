using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GenerateReclaimCsvCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateReclaimCsvCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("generate-reclaim-csv", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "output", true);
            TestHelpers.VerifyCommandOption(command.Options, "include-reclaimed", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task NoMannequins_GenerateEmptyCSV_WithOnlyHeaders()
        {
            const string githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(Array.Empty<Mannequin>());

            string csvContent = null;

            var command = new GenerateReclaimCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = "login,claimantlogin" + Environment.NewLine;

            // Act
            await command.Invoke("octocat", "unit-test-output", false);

            // Assert
            csvContent.Should().Be(expected);
        }

        [Fact]
        public async Task Mannequins_GenerateCSV_UnreclaimedOnly()
        {
            const string githubOrg = "FooOrg";
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
            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);

            string csvContent = null;

            var command = new GenerateReclaimCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = "login,claimantlogin" + Environment.NewLine
                + "mona," + Environment.NewLine;

            // Act
            await command.Invoke(githubOrg, "unit-test-output", false);

            // Assert
            csvContent.Should().Be(expected);
            mockGithubApi.Verify(x => x.GetMannequins(githubOrgId));
        }

        [Fact]
        public async Task Mannequins_GenerateCSV_IncludeAlreadyReclaimed()
        {
            const string githubOrg = "FooOrg";
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
            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequins(githubOrgId).Result).Returns(mannequinsResponse);

            string csvContent = null;

            var command = new GenerateReclaimCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = "login,claimantlogin" + Environment.NewLine
                + "mona," + Environment.NewLine
                + "monalisa,monalisa_gh" + Environment.NewLine;

            // Act
            await command.Invoke(githubOrg, "unit-test-output", true);

            // Assert
            csvContent.Should().Be(expected);

            mockGithubApi.Verify(x => x.GetMannequins(githubOrgId));
        }
    }
}
