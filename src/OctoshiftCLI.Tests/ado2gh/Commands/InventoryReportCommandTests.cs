using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class InventoryReportCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new InventoryReportCommand(null, null, null, null);
            Assert.NotNull(command);
            Assert.Equal("inventory-report", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var orgs = new List<string>() { adoOrg };
            var csvContent = "csv stuff";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns(mockAdoApi.Object);

            var mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
            mockAdoInspectorService.Setup(m => m.GetOrgs(mockAdoApi.Object, null)).ReturnsAsync(orgs);

            var mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
            mockOrgsCsvGenerator.Setup(m => m.Generate(mockAdoApi.Object, orgs)).ReturnsAsync(csvContent);

            var script = "";

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object, mockAdoInspectorService.Object, mockOrgsCsvGenerator.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            await command.Invoke(null, null);

            script.Should().Be(csvContent);
        }

        [Fact]
        public async Task Scoped_To_Single_Org()
        {
            var adoOrg = "FooOrg";
            var orgs = new List<string>() { adoOrg };
            var csvContent = "csv stuff";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns(mockAdoApi.Object);

            var mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
            mockAdoInspectorService.Setup(m => m.GetOrgs(mockAdoApi.Object, adoOrg)).ReturnsAsync(orgs);

            var mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
            mockOrgsCsvGenerator.Setup(m => m.Generate(mockAdoApi.Object, orgs)).ReturnsAsync(csvContent);

            var script = "";

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object, mockAdoInspectorService.Object, mockOrgsCsvGenerator.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            await command.Invoke(adoOrg, null);

            script.Should().Be(csvContent);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            var mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
            var mockOrgsCsvGeneratorService = TestHelpers.CreateMock<OrgsCsvGeneratorService>();

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object, mockAdoInspectorService.Object, mockOrgsCsvGeneratorService.Object);
            await command.Invoke("some org", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
