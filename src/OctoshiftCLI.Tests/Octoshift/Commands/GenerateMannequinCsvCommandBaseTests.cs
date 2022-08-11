using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Models;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class GenerateMannequinCsvCommandBaseTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly GenerateMannequinCsvCommandBase _command;

    private const string CSV_HEADER = "mannequin-user,mannequin-id,target-user";
    private const string GITHUB_ORG = "FooOrg";
    private readonly string GITHUB_ORG_ID = Guid.NewGuid().ToString();
    private string _csvContent = string.Empty;

    public GenerateMannequinCsvCommandBaseTests()
    {
        _command = new GenerateMannequinCsvCommandBase(_mockOctoLogger.Object, _mockGithubApiFactory.Object)
        {
            WriteToFile = (_, contents) =>
            {
                _csvContent = contents;
                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task NoMannequins_GenerateEmptyCSV_WithOnlyHeaders()
    {
        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(GITHUB_ORG_ID).Result).Returns(Array.Empty<Mannequin>());

        var expected = CSV_HEADER + Environment.NewLine;

        // Act
        await _command.Handle("octocat", new FileInfo("unit-test-output"), false);

        // Assert
        _csvContent.Should().Be(expected);
    }

    [Fact]
    public async Task Mannequins_GenerateCSV_UnreclaimedOnly()
    {
        var mannequinsResponse = new[]
        {
            new Mannequin { Id = "monaid", Login = "mona" },
            new Mannequin { Id = "monalisaid", Login = "monalisa", MappedUser = new Claimant { Id = "monalisamapped-id", Login = "monalisa_gh" } }
        };

        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(GITHUB_ORG_ID).Result).Returns(mannequinsResponse);

        var expected = CSV_HEADER + Environment.NewLine
                                  + "mona,monaid," + Environment.NewLine;

        // Act
        await _command.Handle(GITHUB_ORG, new FileInfo("unit-test-output"), false);

        // Assert
        _csvContent.Should().Be(expected);
    }

    [Fact]
    public async Task Mannequins_GenerateCSV_IncludeAlreadyReclaimed()
    {
        var mannequinsResponse = new[]
        {
            new Mannequin { Id = "monaid", Login = "mona" },
            new Mannequin { Id = "monalisaid", Login = "monalisa", MappedUser = new Claimant { Id = "monalisamapped-id", Login = "monalisa_gh" } }
        };

        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.GetMannequins(GITHUB_ORG_ID).Result).Returns(mannequinsResponse);

        var expected = CSV_HEADER + Environment.NewLine
                                  + "mona,monaid," + Environment.NewLine
                                  + "monalisa,monalisaid,monalisa_gh" + Environment.NewLine;

        // Act
        await _command.Handle(GITHUB_ORG, new FileInfo("unit-test-output"), true);

        // Assert
        _csvContent.Should().Be(expected);
    }
}
