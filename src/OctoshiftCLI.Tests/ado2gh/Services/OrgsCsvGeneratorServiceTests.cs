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
        private const string CSV_HEADER = "name,url,owner,teamproject-count,repo-count,pipeline-count";

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_When_Passed_One_Org()
        {
            // Arrange
            var org = "my-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var pipeline = "foo-pipeline";
            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>()
                { { org, new Dictionary<string, IDictionary<string, IEnumerable<string>>>()
                             { { teamProject, new Dictionary<string, IEnumerable<string>>()
                                                   { { repo, new List<string>()
                                                                 { pipeline } } } } } } };
            var ownerName = "Suzy";
            var ownerEmail = "Suzy (suzy@gmail.com)";
            var adoApi = TestHelpers.CreateMock<AdoApi>();

            adoApi.Setup(m => m.GetOrgOwner(org)).ReturnsAsync($"{ownerName} ({ownerEmail})");

            // Act
            var service = new OrgsCsvGeneratorService();
            var result = await service.Generate(adoApi.Object, pipelines);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"{org},https://dev.azure.com/{org},{ownerName} ({ownerEmail}),1,1,1{Environment.NewLine}";

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
