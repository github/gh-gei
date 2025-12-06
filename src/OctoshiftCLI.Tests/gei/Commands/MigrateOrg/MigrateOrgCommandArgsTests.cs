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
        public void Validate_Throws_When_GithubSourceOrg_Is_Url()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = "https://github.com/my-org",
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("The --github-source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        [Fact]
        public void Validate_Throws_When_GithubTargetOrg_Is_Url()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = "https://github.com/my-org",
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = TARGET_PAT,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("The --github-target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        [Fact]
        public void Validate_Throws_When_GithubTargetEnterprise_Is_Url()
        {
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = "https://github.com/enterprises/my-enterprise",
                GithubTargetPat = TARGET_PAT,
            };

            FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should()
                .ThrowExactly<OctoshiftCliException>()
                .WithMessage("The --github-target-enterprise option expects an enterprise name, not a URL. Please provide just the enterprise name (e.g., 'my-enterprise' instead of 'https://github.com/enterprises/my-enterprise').");
        }
    }
}
