using System;
using System.Collections.Generic;
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
        public async Task No_Data()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, new Mock<AdoApiFactory>(null, null, null).Object)
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
                GithubOrg = githubOrg,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().BeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Github_SequentialScript_StartsWithShebang()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";
            const string adoOrg = "foo-ado-org";
            const string adoTeamProject = "foo-team-project";
            const string repo = "foo-repo";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { repo });

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
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
                AdoOrg = adoOrg,
                GithubOrg = githubOrg,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().StartWith("#!/usr/bin/pwsh");
        }

        [Fact]
        public async Task Single_Repo()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";
            const string adoOrg = "foo-ado-org";
            const string adoTeamProject = "foo-team-project";
            const string repo = "foo-repo";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { repo });

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
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
        public async Task Skip_Team_Project_With_No_Repos()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";
            const string adoOrg = "foo-ado-org";
            const string adoTeamProject = "foo-team-project";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoTeamProject });

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
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
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public async Task Single_Repo_Two_Pipelines()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";
            const string adoOrg = "foo-ado-org";
            const string adoTeamProject = "foo-team-project";
            const string repo = "foo-repo";
            const string pipelineOne = "CICD";
            const string pipelineTwo = "Publish";
            const string appId = "app-id";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { repo });
            mockAdoApi
                .Setup(m => m.GetPipelines(adoOrg, adoTeamProject, It.IsAny<string>()))
                .ReturnsAsync(new[] { pipelineOne, pipelineTwo });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(adoOrg, githubOrg, new[] { adoTeamProject }))
                .ReturnsAsync(appId);

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{pipelineOne}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --service-connection-id \"{appId}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{pipelineTwo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --service-connection-id \"{appId}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
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
        public async Task Single_Repo_Two_Pipelines_No_Service_Connection()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";
            const string adoOrg = "foo-ado-org";
            const string adoTeamProject = "foo-team-project";
            const string repo = "foo-repo";
            const string pipelineOne = "CICD";
            const string pipelineTwo = "Publish";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { repo });
            mockAdoApi
                .Setup(m => m.GetPipelines(adoOrg, adoTeamProject, It.IsAny<string>()))
                .ReturnsAsync(new[] { pipelineOne, pipelineTwo });

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            var expected = $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
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
        public async Task Single_Repo_Repos_Only()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";
            const string adoOrg = "foo-ado-org";
            const string adoTeamProject = "foo-team-project";
            const string repo = "foo-repo";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { repo });

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
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
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            script = TrimNonExecutableLines(script);
            var expected = $"Exec {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{repo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{repo}\" --wait }}";

            // Assert
            expected.Should().Be(script);
        }

        [Fact]
        public async Task GetOrgs_All_Orgs()
        {
            var userId = "foo-user";
            var org1 = "foo-1";
            var org2 = "foo-2";
            var orgs = new List<string>() { org1, org2 };

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizations(userId).Result).Returns(orgs);

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetOrgs(mockAdo.Object, null);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == org1);
            Assert.Contains(result, x => x == org2);
        }

        [Fact]
        public async Task GetOrgs_Org_Provided()
        {
            var org1 = "foo-1";

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetOrgs(null, org1);

            Assert.Single(result);
            Assert.Contains(result, x => x == org1);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var teamProject = string.Empty;
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetRepos(mockAdo.Object, orgs, teamProject);

            Assert.Single(result[org][teamProject1]);
            Assert.Single(result[org][teamProject2]);
            Assert.Contains(result[org][teamProject1], x => x == repo1);
            Assert.Contains(result[org][teamProject2], x => x == repo2);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects_With_Team_Project_Supplied()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjectArg = teamProject1;
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetRepos(mockAdo.Object, orgs, teamProjectArg);

            Assert.Single(result[org][teamProjectArg]);
            Assert.False(result[org].ContainsKey(teamProject2));
            Assert.Contains(result[org][teamProjectArg], x => x == repo1);
        }

        [Fact]
        public async Task GetRepos_With_Team_Project_Supplied_Does_Not_Exist()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjectArg = "foo-tp3";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetRepos(mockAdo.Object, orgs, teamProjectArg);

            Assert.Empty(result[org].Keys);
        }

        [Fact]
        public async Task GetPipelines_One_Repo_Two_Pipelines()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();
            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>();
            var pipeline1 = "foo-pipeline-1";
            var pipeline2 = "foo-pipeline-2";

            repos.Add(org, new Dictionary<string, IEnumerable<string>>());
            repos[org].Add(teamProject, new List<string>() { repo });

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetRepoId(org, teamProject, repo).Result).Returns(repoId);
            mockAdo.Setup(x => x.GetPipelines(org, teamProject, repoId).Result).Returns(new List<string>() { pipeline1, pipeline2 });

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetPipelines(mockAdo.Object, repos);

            Assert.Equal(2, result[org][teamProject][repo].Count());
            Assert.Contains(result[org][teamProject][repo], x => x == pipeline1);
            Assert.Contains(result[org][teamProject][repo], x => x == pipeline2);
        }

        [Fact]
        public async Task GetAppIds_With_Team_Project_Supplied_Does_Not_Exist()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var githubOrg = "foo-gh-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetAppIds(mockAdo.Object, orgs, githubOrg);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAppIds_Service_Connect_Exists()
        {
            var org = "foo-org";
            var orgs = new List<string>() { org };
            var githubOrg = "foo-gh-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var appId = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetGithubAppId(org, githubOrg, teamProjects).Result).Returns(appId);

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null);
            var result = await command.GetAppIds(mockAdo.Object, orgs, githubOrg);

            Assert.Equal(appId, result[org]);
        }

        [Fact]
        public async Task GenerateParallelScript_One_Team_Projects_Two_Repos_All_Options()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooRepoId = "FOO_REPO_ID";
            const string fooPipeline = "FOO_PIPELINE";
            const string barRepo = "BAR_REPO";
            const string barRepoId = "BAR_REPO_ID";
            const string barPipeline = "BAR_PIPELINE";
            const string appId = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
            const string githubOrg = "GITHUB_ORG";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(adoOrg))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { fooRepo, barRepo });
            mockAdoApi
                .Setup(m => m.GetRepoId(adoOrg, adoTeamProject, fooRepo))
                .ReturnsAsync(fooRepoId);
            mockAdoApi
                .Setup(m => m.GetRepoId(adoOrg, adoTeamProject, barRepo))
                .ReturnsAsync(barRepoId);
            mockAdoApi
                .Setup(m => m.GetPipelines(adoOrg, adoTeamProject, fooRepoId))
                .ReturnsAsync(new[] { fooPipeline });
            mockAdoApi
                .Setup(m => m.GetPipelines(adoOrg, adoTeamProject, barRepoId))
                .ReturnsAsync(new[] { barPipeline });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(adoOrg, githubOrg, new[] { adoTeamProject }))
                .ReturnsAsync(appId);

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
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
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh share-service-connection --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{barRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{barRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{barRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{fooPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --service-connection-id \"{appId}\" }}");
            expected.AppendLine("    )");
            expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
            expected.AppendLine("} else {");
            expected.AppendLine("    $Failed++");
            expected.AppendLine("}");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {barRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{barRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{barRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh rewire-pipeline --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-pipeline \"{barPipeline}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{barRepo}\" --service-connection-id \"{appId}\" }}");
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
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task GenerateParallelScript_Single_Repo_No_Options()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooRepoId = "FOO_REPO_ID";
            const string fooPipeline = "FOO_PIPELINE";
            const string appId = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
            const string githubOrg = "GITHUB_ORG";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(adoOrg))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { fooRepo });
            mockAdoApi
                .Setup(m => m.GetRepoId(adoOrg, adoTeamProject, fooRepo))
                .ReturnsAsync(fooRepoId);
            mockAdoApi
                .Setup(m => m.GetPipelines(adoOrg, adoTeamProject, fooRepoId))
                .ReturnsAsync(new[] { fooPipeline });
            mockAdoApi
                .Setup(m => m.GetGithubAppId(adoOrg, githubOrg, new[] { adoTeamProject }))
                .ReturnsAsync(appId);

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
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
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
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
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
                Output = new FileInfo("unit-test-output"),
            };
            await command.Invoke(args);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task GenerateParallelScript_No_Data()
        {
            // Arrange
            const string githubOrg = "foo-gh-org";

            string script = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, new Mock<AdoApiFactory>(null, null, null).Object)
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
                GithubOrg = githubOrg,
                Sequential = true,
                Output = new FileInfo("unit-test-output")
            };
            await command.Invoke(args);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public async Task GenerateParallelScript_Single_Repo_No_Service_Connection_All_Options()
        {
            // Arrange
            const string adoOrg = "ADO_ORG";
            const string adoTeamProject = "ADO_TEAM_PROJECT";
            const string fooRepo = "FOO_REPO";
            const string fooRepoId = "FOO_REPO_ID";
            const string fooPipeline = "FOO_PIPELINE";
            const string barPipeline = "BAR_PIPELINE";
            const string githubOrg = "GITHUB_ORG";

            var mockAdoApi = new Mock<AdoApi>(null);
            mockAdoApi
                .Setup(m => m.GetOrganizations(It.IsAny<string>()))
                .ReturnsAsync(new[] { adoOrg });
            mockAdoApi
                .Setup(m => m.GetTeamProjects(adoOrg))
                .ReturnsAsync(new[] { adoTeamProject });
            mockAdoApi
                .Setup(m => m.GetEnabledRepos(adoOrg, adoTeamProject))
                .ReturnsAsync(new[] { fooRepo });
            mockAdoApi
                .Setup(m => m.GetRepoId(adoOrg, adoTeamProject, fooRepo))
                .ReturnsAsync(fooRepoId);
            mockAdoApi
                .Setup(m => m.GetPipelines(adoOrg, adoTeamProject, fooRepoId))
                .ReturnsAsync(new[] { fooPipeline, barPipeline });

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdoApi.Object);

            string actual = null;
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object)
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
            expected.AppendLine($"# =========== Queueing migration for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
            expected.AppendLine();
            expected.AppendLine($"# === Queueing repo migrations for Team Project: {adoOrg}/{adoTeamProject} ===");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Maintainers\" --idp-group \"{adoTeamProject}-Maintainers\" }}");
            expected.AppendLine($"Exec {{ ./ado2gh create-team --github-org \"{githubOrg}\" --team-name \"{adoTeamProject}-Admins\" --idp-group \"{adoTeamProject}-Admins\" }}");
            expected.AppendLine();
            expected.AppendLine($"Exec {{ ./ado2gh lock-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ ./ado2gh migrate-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {adoOrg} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {adoTeamProject} and Repo: {fooRepo}. Will then complete the below post migration steps. ===");
            expected.AppendLine($"./ado2gh wait-for-migration --migration-id $RepoMigrations[\"{adoOrg}/{adoTeamProject}-{fooRepo}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) {");
            expected.AppendLine("    ExecBatch @(");
            expected.AppendLine($"        {{ ./ado2gh disable-ado-repo --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{fooRepo}\" }}");
            expected.AppendLine($"        {{ ./ado2gh configure-autolink --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Maintainers\" --role \"maintain\" }}");
            expected.AppendLine($"        {{ ./ado2gh add-team-to-repo --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" --team \"{adoTeamProject}-Admins\" --role \"admin\" }}");
            expected.AppendLine($"        {{ ./ado2gh integrate-boards --ado-org \"{adoOrg}\" --ado-team-project \"{adoTeamProject}\" --github-org \"{githubOrg}\" --github-repo \"{adoTeamProject}-{fooRepo}\" }}");
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
                GithubOrg = githubOrg,
                AdoOrg = adoOrg,
                Output = new FileInfo("unit-test-output"),
                All = true
            };
            await command.Invoke(args);

            // Assert
            actual.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            // Arrange
            const string adoPat = "ado-pat";

            var mockAdoApi = new Mock<AdoApi>(null);
            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdoApi.Object);

            // Act
            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
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

        private string TrimNonExecutableLines(string script)
        {
            var lines = script.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.Trim().StartsWith("#"));
            // This skips the Exec function definition
            lines = lines.Skip(9);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
