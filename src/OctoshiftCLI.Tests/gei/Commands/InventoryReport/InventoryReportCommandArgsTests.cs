using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.InventoryReport
{
    public class InventoryReportCommandArgsTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private const string ORG = "FOO-SOURCE-ORG";

        [Fact]
        public void NoSslVerify_Must_Be_False_When_GhesApiUrl_Not_Set()
        {
            var args = new InventoryReportCommandArgs
            {
                GithubOrg = ORG,
                NoSslVerify = true,
            };

            FluentActions
                .Invoking(() => args.Validate(_mockOctoLogger.Object))
                .Should().Throw<OctoshiftCliException>();
        }
    }
}
