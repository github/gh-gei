using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateOrg;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateOrg
{
    public class MigrateOrgCommandArgsTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private const string TARGET_ENTERPRISE = "foo-target-ent";
        private const string SOURCE_ORG = "foo-source-org";
        private const string TARGET_ORG = "foo-target-org";
        private const string SOURCE_PAT = "foo-source-pat";
        private const string TARGET_PAT = "foo-target-pat";

        [Fact]
        public void Validates_Wait_And_QueueOnly_Not_Passed_Together()
        {
            // Act
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubSourcePat = SOURCE_PAT,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
                Wait = true,
                QueueOnly = true,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                               .Should()
                               .ThrowExactly<OctoshiftCliException>()
                               .WithMessage("*wait*");
        }

        [Fact]
        public void Wait_Flag_Shows_Warning()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubSourcePat = SOURCE_PAT,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
                Wait = true,
            };

            args.Validate(_mockOctoLogger.Object);

            _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(x => x.ToLower().Contains("wait"))));
        }

        [Fact]
        public void No_Wait_And_No_Queue_Only_Flags_Shows_Warning()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubSourcePat = SOURCE_PAT,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
                Wait = false,
                QueueOnly = false,
            };

            args.Validate(_mockOctoLogger.Object);

            _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(x => x.ToLower().Contains("wait"))));
        }

        [Fact]
        public void Source_Pat_Defaults_To_Target_Pat()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
                Wait = true,
            };

            args.Validate(_mockOctoLogger.Object);

            args.GithubSourcePat.Should().Be(TARGET_PAT);
        }
    }
}
