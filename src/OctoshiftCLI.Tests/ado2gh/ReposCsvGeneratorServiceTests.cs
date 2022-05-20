using System;
using System.Collections.Generic;
using FluentAssertions;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ReposCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "org,teamproject,repo";

        [Fact]
        public void Generate_Should_Return_Correct_Csv_When_Passed_One_Org()
        {
            // Arrange
            var org = "my-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var repos = new Dictionary<string, IDictionary<string, IEnumerable<string>>>() { { org, new Dictionary<string, IEnumerable<string>>() { { teamProject, new List<string>() { repo } } } } };

            // Act
            var service = new ReposCsvGeneratorService();
            var result = service.Generate(repos);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"{org},{teamProject},{repo}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public void Generate_Should_Return_Correct_Csv_When_Passed_Null_Orgs()
        {
            // Act
            var service = new ReposCsvGeneratorService();
            var result = service.Generate(null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            result.Should().Be(expected);
        }
    }
}
