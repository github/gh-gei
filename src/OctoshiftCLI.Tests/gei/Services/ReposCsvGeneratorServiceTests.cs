using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Services
{
    public class ReposCsvGeneratorServiceTests
    {
        private const string FULL_CSV_HEADER = "org,repo,url,visibility,last-push-date,compressed-repo-size-in-bytes,pr-count,commits-on-default-branch,most-active-contributor";
        private const string MINIMAL_CSV_HEADER = "org,repo,url,visibility,last-push-date,compressed-repo-size-in-bytes,pr-count,commits-on-default-branch";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();

        private const string ORG = "foo-org";
        private const string API_URL = "https://github.contoso.com/api/v3";

        private readonly ReposCsvGeneratorService _service;

        public ReposCsvGeneratorServiceTests()
        {
            _service = new ReposCsvGeneratorService(_mockOctoLogger.Object, _mockGithubApi.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Repo()
        {
            // Arrange
            var repo = "some-repo";
            var url = $"https://github.com/{ORG}/_git/{repo}";
            var visibility = "internal";
            var lastCommitDate = DateTime.Now;
            var repoSize = 123;
            var prCount = 71;
            var commitCount = 832;
            var authors = new List<string>() { "Roger Rabbit", "Fred Flintstone", "Fred Flintstone" };
            var mostActiveContributor = "Fred Flintstone";
            var expectedSinceDate = DateTime.Today.AddYears(-1);

            _mockGithubApi.Setup(m => m.GetRepos(ORG)).ReturnsAsync(new List<(string Name, string Visibility, long Size)>() { (repo, visibility, repoSize) });
            _mockGithubApi.Setup(m => m.GetCommitInfo(ORG, repo)).ReturnsAsync((false, commitCount, lastCommitDate));
            _mockGithubApi.Setup(m => m.GetAuthorsSince(ORG, repo, It.Is<DateTime>(x => Math.Abs(x.Subtract(expectedSinceDate).Days) <= 2))).ReturnsAsync(new List<string>() { mostActiveContributor });
            _mockGithubApi.Setup(m => m.GetPullRequestCount(ORG, repo)).ReturnsAsync(prCount);

            // Act
            var result = await _service.Generate(null, ORG);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ORG}\",\"{repo}\",\"{url}\",\"{visibility}\",\"{lastCommitDate:dd-MMM-yyyy hh:mm tt}\",{repoSize},{prCount},{commitCount},\"{mostActiveContributor}\"{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Filter_Out_ActiveContributor_With_Bot_In_The_Name()
        {
            // Arrange
            var repo = "some-repo";
            var url = $"https://github.com/{ORG}/_git/{repo}";
            var visibility = "internal";
            var lastCommitDate = DateTime.Now;
            var repoSize = 123;
            var prCount = 71;
            var commitCount = 832;
            var authors = new List<string>() { "Roger Rabbit", "Build Service [bot]", "Build Service [bot]" };
            var mostActiveContributor = "Roger Rabbit";
            var expectedSinceDate = DateTime.Today.AddYears(-1);

            _mockGithubApi.Setup(m => m.GetRepos(ORG)).ReturnsAsync(new List<(string Name, string Visibility, long Size)>() { (repo, visibility, repoSize) });
            _mockGithubApi.Setup(m => m.GetCommitInfo(ORG, repo)).ReturnsAsync((false, commitCount, lastCommitDate));
            _mockGithubApi.Setup(m => m.GetAuthorsSince(ORG, repo, It.Is<DateTime>(x => Math.Abs(x.Subtract(expectedSinceDate).Days) <= 2))).ReturnsAsync(authors);
            _mockGithubApi.Setup(m => m.GetPullRequestCount(ORG, repo)).ReturnsAsync(prCount);

            // Act
            var result = await _service.Generate(null, ORG);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ORG}\",\"{repo}\",\"{url}\",\"{visibility}\",\"{lastCommitDate:dd-MMM-yyyy hh:mm tt}\",{repoSize},{prCount},{commitCount},\"{mostActiveContributor}\"{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Minimal_Csv_When_Minimal_Arg_Is_True()
        {
            // Arrange
            var repo = "some-repo";
            var url = $"https://github.com/{ORG}/_git/{repo}";
            var visibility = "internal";
            var lastCommitDate = DateTime.Now;
            var repoSize = 123;
            var prCount = 71;
            var commitCount = 832;

            _mockGithubApi.Setup(m => m.GetRepos(ORG)).ReturnsAsync(new List<(string Name, string Visibility, long Size)>() { (repo, visibility, repoSize) });
            _mockGithubApi.Setup(m => m.GetCommitInfo(ORG, repo)).ReturnsAsync((false, commitCount, lastCommitDate));
            _mockGithubApi.Setup(m => m.GetPullRequestCount(ORG, repo)).ReturnsAsync(prCount);

            // Act
            var result = await _service.Generate(null, ORG, minimal: true);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ORG}\",\"{repo}\",\"{url}\",\"{visibility}\",\"{lastCommitDate:dd-MMM-yyyy hh:mm tt}\",{repoSize},{prCount},{commitCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }
    }
}
