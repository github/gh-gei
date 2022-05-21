using System;
using System.Collections.Generic;
using FluentAssertions;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class TeamProjectsCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "org,teamproject,repo-count,pipeline-count";

        [Fact]
        public void Generate_Should_Return_Correct_Csv_When_Passed_One_Org()
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

            // Act
            var service = new TeamProjectsCsvGeneratorService();
            var result = service.Generate(pipelines);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"{org},{teamProject},1,1{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public void Generate_Should_Return_Correct_Csv_When_Passed_Null_Orgs()
        {
            // Act
            var service = new TeamProjectsCsvGeneratorService();
            var result = service.Generate(null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            result.Should().Be(expected);
        }
    }
}
