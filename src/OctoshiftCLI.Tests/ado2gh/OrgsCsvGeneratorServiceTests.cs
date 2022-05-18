using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class OrgsCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "name,owner";

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_When_Passed_One_Org()
        {
            // Arrange
            var orgName = "my-org";
            var orgs = new List<string>() { orgName };
            var ownerName = "Suzy";
            var ownerEmail = "Suzy (suzy@gmail.com)";
            var adoApi = TestHelpers.CreateMock<AdoApi>();

            adoApi.Setup(m => m.GetOrgOwner(orgName)).ReturnsAsync($"{ownerName} ({ownerEmail})");

            // Act
            var service = new OrgsCsvGeneratorService();
            var result = await service.Generate(adoApi.Object, orgs);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"{orgName},{ownerName} ({ownerEmail}){Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_When_Passed_Null_Orgs()
        {
            // Act
            var service = new OrgsCsvGeneratorService();
            var result = await service.Generate(null, null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            result.Should().Be(expected);
        }
    }
}
