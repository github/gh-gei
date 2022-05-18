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
        private const string CSV_HEADER = "mannequin-user,mannequin-id,target-user";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateMannequinCsvCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("generate-mannequin-csv", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "include-reclaimed", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task NoMannequins_GenerateEmptyCSV_WithOnlyHeaders()
        {
            const string githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationIdAsync(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequinsAsync(githubOrgId).Result).Returns(Array.Empty<Mannequin>());

            var csvContent = "";

            var command = new GenerateMannequinCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockTargetGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };
            var expected = CSV_HEADER + Environment.NewLine;

            // Act
            await command.Invoke("octocat", new FileInfo("unit-test-output"), false);

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
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);
            mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationIdAsync(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequinsAsync(githubOrgId).Result).Returns(mannequinsResponse);

            var csvContent = "";

            var command = new GenerateMannequinCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockTargetGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = CSV_HEADER + Environment.NewLine
                + "mona,monaid," + Environment.NewLine;

            // Act
            await command.Invoke(githubOrg, new FileInfo("unit-test-output"), false);

            // Assert
            csvContent.Should().Be(expected);
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
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            mockGithubApi.Setup(x => x.GetOrganizationIdAsync(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(x => x.GetMannequinsAsync(githubOrgId).Result).Returns(mannequinsResponse);

            var csvContent = "";

            var command = new GenerateMannequinCsvCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockTargetGithubApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    csvContent = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = CSV_HEADER + Environment.NewLine
                + "mona,monaid," + Environment.NewLine
                + "monalisa,monalisaid,monalisa_gh" + Environment.NewLine;

            // Act
            await command.Invoke(githubOrg, new FileInfo("unit-test-output"), true);

            // Assert
            csvContent.Should().Be(expected);
        }
    }
}
