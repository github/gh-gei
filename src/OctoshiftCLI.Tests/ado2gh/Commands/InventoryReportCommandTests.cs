using System;
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
            var command = new InventoryReportCommand(null, null, null);
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
            var userId = Guid.NewGuid().ToString();
            var csvContent = "csv stuff";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            mockAdo.Setup(x => x.GetUserId()).ReturnsAsync(userId);
            mockAdo.Setup(x => x.GetOrganizations(userId)).ReturnsAsync(orgs);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns(mockAdo.Object);

            var mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
            mockOrgsCsvGenerator.Setup(m => m.Generate(mockAdo.Object, orgs)).ReturnsAsync(csvContent);

            var script = "";

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object, mockOrgsCsvGenerator.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            await command.Invoke(null);

            script.Should().Be(csvContent);
        }

        [Fact]
        public async Task Scoped_To_Single_Org()
        {
            var adoOrg = "FooOrg";
            var orgs = new List<string>() { adoOrg };
            var userId = Guid.NewGuid().ToString();
            var csvContent = "csv stuff";

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns((AdoApi)null);

            var mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
            mockOrgsCsvGenerator.Setup(m => m.Generate(null, orgs)).ReturnsAsync(csvContent);

            var script = "";

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object, mockOrgsCsvGenerator.Object)
            {
                WriteToFile = (_, contents) =>
                {
                    script = contents;
                    return Task.CompletedTask;
                }
            };

            await command.Invoke(adoOrg);

            script.Should().Be(csvContent);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            var mockOrgsCsvGeneratorService = TestHelpers.CreateMock<OrgsCsvGeneratorService>();

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object, mockOrgsCsvGeneratorService.Object);
            await command.Invoke("some org", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
