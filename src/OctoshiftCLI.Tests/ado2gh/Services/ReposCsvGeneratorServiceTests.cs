using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ReposCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "org,teamproject,repo,url,pipeline-count";

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_When_Passed_One_Org()
        {
            // Arrange
            var org = "my org";
            var teamProject = "foo tp";
            var repo = "foo repo";
            var pipeline = "foo-pipeline";
            var pipelines = new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>()
                { { org, new Dictionary<string, IDictionary<string, IEnumerable<string>>>()
                             { { teamProject, new Dictionary<string, IEnumerable<string>>()
                                                   { { repo, new List<string>()
                                                                 { pipeline } } } } } } };

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();

            // Act
            var service = new ReposCsvGeneratorService();
            var result = await service.Generate(mockAdoApi.Object, pipelines);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"{org},{teamProject},{repo},https://dev.azure.com/my%20org/foo%20tp/_git/foo%20repo,1{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_When_Passed_Null_Orgs()
        {
            // Act
            var service = new ReposCsvGeneratorService();
            var result = await service.Generate(null, null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            result.Should().Be(expected);
        }
    }
}
