using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class IntegrateBoardsCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new IntegrateBoardsCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("integrate-boards", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repos", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task HappyPath()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepos = "foo,blah";
            var githubReposList = new List<string>() { "foo", "blah" };
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var endpointId = Guid.NewGuid().ToString();
            var repoIds = new List<string>() { "12", "34" };
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizationId(userId, adoOrg).Result).Returns(orgId);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, orgId, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.CreateEndpoint(adoOrg, teamProjectId, githubToken, githubHandle).Result).Returns(endpointId);
            mockAdo.Setup(x => x.GetGithubRepoIds(adoOrg, orgId, adoTeamProject, teamProjectId, endpointId, githubOrg, githubReposList).Result).Returns(repoIds);

            using var adoFactory = new AdoApiFactory(mockAdo.Object);
            using var githubFactory = new GithubApiFactory(githubToken);

            var command = new IntegrateBoardsCommand(new OctoLogger(), adoFactory, githubFactory);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepos);

            mockAdo.Verify(x => x.CreateBoardsGithubConnection(adoOrg, orgId, adoTeamProject, endpointId, repoIds));
        }
    }
}