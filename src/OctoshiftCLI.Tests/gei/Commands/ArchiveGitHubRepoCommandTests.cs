using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

// ReSharper disable once CheckNamespace
namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class ArchiveGitHubRepoCommandTests
{
    public ArchiveGitHubRepoCommandTests() =>
        _command = new ArchiveGitHubRepoCommand(
            _mockOctoLogger.Object,
            _mockSourceGithubApiFactory.Object,
            _mockEnvironmentVariableProvider.Object);

    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<ISourceGithubApiFactory> _mockSourceGithubApiFactory = new();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly ArchiveGitHubRepoCommand _command;

    [Fact]
    public async Task Happy_Path_For_Ghec()
    {
        var sourceOrg = Guid.NewGuid().ToString();
        var sourceRepo = Guid.NewGuid().ToString();
        var sourceGithubPat = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(_ => _.ArchiveRepository(sourceOrg, sourceRepo)).Verifiable("`ArchiveRepository` call did not match specifications");

        _mockSourceGithubApiFactory
            .Setup(_ => _.Create(null, sourceGithubPat))
            .Returns(_mockGithubApi.Object);

        var args = new ArchiveGitHubRepoCommandArgs
        {
            GithubSourceOrg = sourceOrg,
            GithubSourcePat = sourceGithubPat,
            SourceRepo = sourceRepo
        };

        await _command.Invoke(args);
    }

    [Fact]
    public async Task Happy_Path_For_Ghes()
    {
        var ghesApiUrl = "https://myghes.local/api/v3";
        var sourceOrg = Guid.NewGuid().ToString();
        var sourceRepo = Guid.NewGuid().ToString();
        var sourceGithubPat = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(_ => _.ArchiveRepository(sourceOrg, sourceRepo)).Verifiable("`ArchiveRepository` call did not match specifications");

        _mockSourceGithubApiFactory
            .Setup(_ => _.Create(ghesApiUrl, sourceGithubPat))
            .Returns(_mockGithubApi.Object);

        var args = new ArchiveGitHubRepoCommandArgs
        {
            GhesApiUrl = ghesApiUrl,
            GithubSourceOrg = sourceOrg,
            GithubSourcePat = sourceGithubPat,
            SourceRepo = sourceRepo
        };

        await _command.Invoke(args);
    }

    [Fact]
    public async Task Happy_Path_For_Ghes_With_No_Ssl_Verify()
    {
        var ghesApiUrl = "https://myghes.local/api/v3";
        var sourceOrg = Guid.NewGuid().ToString();
        var sourceRepo = Guid.NewGuid().ToString();
        var sourceGithubPat = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(_ => _.ArchiveRepository(sourceOrg, sourceRepo)).Verifiable("`ArchiveRepository` call did not match specifications");

        _mockSourceGithubApiFactory
            .Setup(_ => _.CreateClientNoSsl(ghesApiUrl, sourceGithubPat))
            .Returns(_mockGithubApi.Object);

        var args = new ArchiveGitHubRepoCommandArgs
        {
            GhesApiUrl = ghesApiUrl,
            GithubSourceOrg = sourceOrg,
            GithubSourcePat = sourceGithubPat,
            SourceRepo = sourceRepo,
            NoSslVerify = true
        };

        await _command.Invoke(args);
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("archive-gh-repo");
        _command.Options.Count.Should().Be(6);

        TestHelpers.VerifyCommandOption(_command.Options, "github-source-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "github-source-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "source-repo", true);
        TestHelpers.VerifyCommandOption(_command.Options, "ghes-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }
}
