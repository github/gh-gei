using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class IntegrateBoardsCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new IntegrateBoardsCommand();
            Assert.NotNull(command);
            Assert.Equal("integrate-boards", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repos", true);
        }

        [Fact]
        public async Task HappyPath()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepos = "foo,blah";
            var githubReposList = new List<string>() { "foo", "blah" };
            var adoToken = Guid.NewGuid().ToString();
            var githubToken = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var endpointId = Guid.NewGuid().ToString();
            var repoIds = new List<string>() { "12", "34" };

            var mockAdo = new Mock<AdoApi>(string.Empty);
            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizationId(userId, adoOrg).Result).Returns(orgId);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, orgId, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.CreateEndpoint(adoOrg, teamProjectId, githubToken, githubHandle).Result).Returns(endpointId);
            mockAdo.Setup(x => x.GetGithubRepoIds(adoOrg, orgId, adoTeamProject, teamProjectId, endpointId, githubOrg, githubReposList).Result).Returns(repoIds);

            Environment.SetEnvironmentVariable("ADO_PAT", adoToken);
            AdoApiFactory.Create = token => token == adoToken ? mockAdo.Object : null;

            Environment.SetEnvironmentVariable("GH_PAT", githubToken);

            var command = new IntegrateBoardsCommand();
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepos);

            mockAdo.Verify(x => x.CreateBoardsGithubConnection(adoOrg, orgId, adoTeamProject, endpointId, repoIds));
        }

        [Fact]
        public async Task MissingADOPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            AdoApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("ADO_PAT", string.Empty);

            var command = new DisableRepoCommand();

            await command.Invoke("foo", "foo", "foo");
        }

        [Fact]
        public async Task MissingGithubPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("GH_PAT", string.Empty);

            var command = new CreateTeamCommand();

            await command.Invoke("foo", "foo", "foo");
        }
    }
}
