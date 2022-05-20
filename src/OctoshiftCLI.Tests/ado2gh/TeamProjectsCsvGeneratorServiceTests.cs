using System;
using System.Collections.Generic;
using FluentAssertions;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class TeamProjectsCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "org,teamproject";

        [Fact]
        public void Generate_Should_Return_Correct_Csv_When_Passed_One_Org()
        {
            // Arrange
            var orgName = "my-org";
            var teamProject = "foo-tp";
            var teamProjects = new Dictionary<string, IEnumerable<string>>() { { orgName, new List<string>() { teamProject } } };

            // Act
            var service = new TeamProjectsCsvGeneratorService();
            var result = service.Generate(teamProjects);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"{orgName},{teamProject}{Environment.NewLine}";

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
