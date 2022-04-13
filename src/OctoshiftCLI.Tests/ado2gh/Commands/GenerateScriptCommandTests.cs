using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GenerateScriptCommandTests
    {
        private const string ADO_ORG = "ADO_ORG";
        private const string ADO_TEAM_PROJECT = "ADO_TEAM_PROJECT";
        private const string FOO_REPO = "FOO_REPO";
        private const string FOO_REPO_ID = "FOO_REPO_ID";
        private const string FOO_PIPELINE = "FOO_PIPELINE";
        private const string BAR_REPO = "BAR_REPO";
        private const string BAR_REPO_ID = "BAR_REPO_ID";
        private const string BAR_PIPELINE = "BAR_PIPELINE";
        private const string APP_ID = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
        private const string GITHUB_ORG = "GITHUB_ORG";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(16);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "create-teams", false);
            TestHelpers.VerifyCommandOption(command.Options, "link-idp-groups", false);
            TestHelpers.VerifyCommandOption(command.Options, "lock-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "disable-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "add-teams-to-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "integrate-boards", false);
            TestHelpers.VerifyCommandOption(command.Options, "rewire-pipelines", false);
            TestHelpers.VerifyCommandOption(command.Options, "all", false);
        }

        [Fact]
        public async Task Invoke_Gets_All_Orgs_When_Ado_Org_Is_Not_Provided()
        {
            // Arrange
            const string userId = "USER_ID";
            const string anotherAdoOrg = "ANOTHER_ADO_ORG";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetUserId())
                .ReturnsAsync(userId);
            mockAdoApi
                .Setup(m => m.GetOrganizations(userId))
                .ReturnsAsync(new[] { ADO_ORG, anotherAdoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(anotherAdoOrg))
                .ReturnsAsync(Array.Empty<string>());
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().NotBeEmpty();
            mockAdoApi.Verify(m => m.GetUserId(), Times.Once);
            mockAdoApi.Verify(m => m.GetOrganizations(userId), Times.Once);
            mockAdoApi.Verify(m => m.GetTeamProjects(ADO_ORG), Times.Once);
            mockAdoApi.Verify(m => m.GetTeamProjects(anotherAdoOrg), Times.Once);
            mockAdoApi.Verify(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT), Times.Once);
            mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_Does_Not_Get_All_Orgs_When_Ado_Org_Is_Provided()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().NotBeEmpty();
            mockAdoApi.Verify(m => m.GetTeamProjects(ADO_ORG), Times.Once);
            mockAdoApi.Verify(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT), Times.Once);
            mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_Gets_All_Repos_For_Each_Team_Project_When_Ado_Team_Project_Is_Not_Provided()
        {
            // Arrange
            const string anotherTeamProject = "ANOTHER_TEAM_PROJECT";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT, anotherTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, anotherTeamProject))
                .ReturnsAsync(new[] { BAR_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().NotBeEmpty();
            mockAdoApi.Verify(m => m.GetTeamProjects(ADO_ORG), Times.Once);
            mockAdoApi.Verify(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT), Times.Once);
            mockAdoApi.Verify(m => m.GetEnabledRepos(ADO_ORG, anotherTeamProject), Times.Once);
            mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_Gets_All_Repos_For_Provided_Ado_Team_Project()
        {
            // Arrange
            const string anotherTeamProject = "ANOTHER_TEAM_PROJECT";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT, anotherTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().NotBeEmpty();
            mockAdoApi.Verify(m => m.GetTeamProjects(ADO_ORG), Times.Once);
            mockAdoApi.Verify(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT), Times.Once);
            mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_Gets_No_Repos_When_Provided_Ado_Team_Project_Is_Not_Found()
        {
            // Arrange
            const string anotherTeamProject = "ANOTHER_TEAM_PROJECT";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT, anotherTeamProject });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                AdoTeamProject = "NOT_EXISTING_TEAM_PROJECT",
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            TrimNonExecutableLines(script).Should().BeEmpty();
            mockAdoApi.Verify(m => m.GetTeamProjects(ADO_ORG), Times.Once);
            mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SequentialScript_No_Data()
        {
            // Arrange
            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, TestHelpers.CreateMock<AdoApiFactory>().Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().BeNullOrWhiteSpace();
        }

        [Fact]
        public async Task SequentialScript_StartsWith_Shebang()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoOrg = ADO_ORG,
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().StartWith("#!/usr/bin/pwsh");
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_No_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);
            var expected = $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";

            // Assert
            expected.Should().Be(script);
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_All_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            expected.Should().Be(script);
        }

        [Fact]
        public async Task SequentialScript_Skips_Team_Project_With_No_Repos()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_Two_Pipelines_All_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE, BAR_PIPELINE });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT }))
                .ReturnsAsync(APP_ID);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{BAR_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert

            expected.Should().Be(script);
        }

        [Fact]
        public async Task SequentialScript_Single_Repo_Two_Pipelines_No_Service_Connection_All_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE, BAR_PIPELINE });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            expected.Should().Be(script);
        }

        [Fact]
        public async Task SequentialScript_Create_Teams_Option_Should_Generate_Create_Teams_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.Append($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                CreateTeams = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Link_Idp_Groups_Option_Should_Generate_Create_Teams_Scripts_With_Idp_Groups()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.Append($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                LinkIdpGroups = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Lock_Ado_Repo_Option_Should_Generate_Lock_Ado_Repo_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.Append($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                LockAdoRepos = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Disable_Ado_Repo_Option_Should_Generate_Disable_Ado_Repo_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.Append($"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                DisableAdoRepos = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Add_Teams_To_Repos_Option_Should_Generate_Add_Teams_To_Repos_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.AppendLine($"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.Append($"Exec {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                AddTeamsToRepos = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Integrate_Boards_Option_Should_Generate_Auto_Link_And_Boards_Integration_Scripts()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.AppendLine($"Exec {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.Append($"Exec {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                IntegrateBoards = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task SequentialScript_Rewire_Pipelines_Option_Should_Generate_Share_Service_Connection_And_Rewire_Pipeline_Scripts()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT }))
                .ReturnsAsync(APP_ID);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --wait }}");
            expected.Append($"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output"),
                RewirePipelines = true
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().Contain(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_No_Data()
        {
            // Arrange
            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, TestHelpers.CreateMock<AdoApiFactory>().Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public async Task ParallelScript_StartsWith_Shebang()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoOrg = ADO_ORG,
                GithubOrg = GITHUB_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().StartWith("#!/usr/bin/pwsh");
        }

        [Fact]
        public async Task ParallelScript_Single_Repo_No_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT }))
                .ReturnsAsync(APP_ID);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task PatallelScript_Skips_Team_Project_With_No_Repos()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script, 37);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public async Task ParallelScript_Two_Repos_Two_Pipelines_All_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO, BAR_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO))
                .ReturnsAsync(BAR_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE });
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO_ID))
                .ReturnsAsync(new[] { BAR_PIPELINE });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT }))
                .ReturnsAsync(APP_ID);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {BAR_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{BAR_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Single_Repo_No_Service_Connection_All_Options()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE, BAR_PIPELINE });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/pwsh");
            expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
            expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine("Write-Host =============== Summary ===============");
            expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
            expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
            expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
            expected.AppendLine();
            expected.AppendLine();

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Create_Teams_Option_Should_Generate_Create_Teams_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                CreateTeams = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Link_Idp_Groups_Option_Should_Generate_Create_Teams_Scripts_With_Idp_Groups()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                LinkIdpGroups = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Lock_Ado_Repo_Option_Should_Generate_Lock_Ado_Repo_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    $Succeeded++");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                LockAdoRepos = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Disable_Ado_Repo_Option_Should_Generate_Disable_Ado_Repo_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                DisableAdoRepos = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Add_Teams_To_Repos_Option_Should_Generate_Add_Teams_To_Repos_Script()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                AddTeamsToRepos = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Integrate_Boards_Option_Should_Generate_Auto_Link_And_Boards_Integration_Scripts()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                IntegrateBoards = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task ParallelScript_Rewire_Pipelines_Option_Should_Generate_Share_Service_Connection_And_Rewire_Pipeline_Scripts()
        {
            // Arrange
            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            mockAdoApi
                .Setup(m => m.GetTeamProjects(ADO_ORG))
                .ReturnsAsync(new[] { ADO_TEAM_PROJECT });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .ReturnsAsync(new[] { FOO_REPO });
            mockAdoApi
                .Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .ReturnsAsync(FOO_REPO_ID);
            mockAdoApi
                .Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO_ID))
                .ReturnsAsync(new[] { FOO_PIPELINE });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT }))
                .ReturnsAsync(APP_ID);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    actual = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.Append('}');

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                AdoOrg = ADO_ORG,
                Output = new FileInfo("unit-test-output"),
                RewirePipelines = true
            };
            await command.Invoke(args);

            actual = TrimNonExecutableLines(actual, 34, 3);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            // Arrange
            const string adoPat = "ado-pat";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdoApi.Object);

            // Act
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object);
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = "githubOrg",
                AdoOrg = "adoOrg",
                AdoPat = adoPat,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }

        private string TrimNonExecutableLines(string script, int skipFirst = 9, int skipLast = 0)
        {
            var lines = script.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !x.Trim().StartsWith("#"))
                .Where(x => !x.Trim().StartsWith("Write-Host", StringComparison.OrdinalIgnoreCase));
            lines = lines.Skip(skipFirst);
            lines = lines.SkipLast(skipLast);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
