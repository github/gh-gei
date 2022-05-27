using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class DisableRepoCommandTests
    {
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly DisableRepoCommand _command;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_REPO = "foo-repo";
        private readonly string REPO_ID = Guid.NewGuid().ToString();

        public DisableRepoCommandTests()
        {
            _command = new DisableRepoCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("disable-ado-repo", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var repos = new List<(string Id, string Name, bool IsDisabled)> { (REPO_ID, ADO_REPO, false) };

            _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(repos);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

            _mockAdoApi.Verify(x => x.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, REPO_ID));
        }

        [Fact]
        public async Task Idempotency_Repo_Disabled()
        {
            var repos = new List<(string Id, string Name, bool IsDisabled)> { (REPO_ID, ADO_REPO, true) };

            _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(repos);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

            _mockAdoApi.Verify(x => x.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, REPO_ID), Times.Never);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var repos = new[] { ("repoId", "adoRepo", true) };
            _mockAdoApi.Setup(x => x.GetRepos(It.IsAny<string>(), It.IsAny<string>()).Result).Returns(repos);
            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke("adoOrg", "adoTeamProject", "adoRepo", adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
