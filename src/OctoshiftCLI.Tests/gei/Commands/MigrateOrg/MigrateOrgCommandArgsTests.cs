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
        private const string TARGET_PAT = "foo-target-pat";

        [Fact]
        public void Source_Pat_Defaults_To_Target_Pat()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
            };

            args.Validate(_mockOctoLogger.Object);

            args.GithubSourcePat.Should().Be(TARGET_PAT);
        }
        [Fact]
        public void Throws_If_Url_Passed_In_GithubSourceOrg()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = "https://github.com/foo",
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*GithubSourceOrg should be an org name, not a URL*");
        }

        [Fact]
        public void Throws_If_Url_Passed_In_GithubTargetOrg()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = "https://github.com/foo",
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*GithubTargetOrg should be an org name, not a URL*");
        }

        [Fact]
        public void Throws_If_Url_Passed_In_Both_Source_And_Target_Org()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = "https://github.com/foo",
                GithubTargetOrg = "https://github.com/bar",
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("GithubSourceOrg should be an org name, not a URL.");
        }
        [Fact]
        public void Throws_If_Url_Passed_In_GithubTargetEnterprise()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = "https://github.com/foo",
                GithubTargetPat = TARGET_PAT,
            };
            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("*GithubTargetEnterprise should be an enterprise name, not a URL*");
        }
    }
}
