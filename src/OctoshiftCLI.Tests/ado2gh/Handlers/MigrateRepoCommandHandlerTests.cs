using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.AdoToGithub.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands;

public class MigrateRepoCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

    private readonly MigrateRepoCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private const string ADO_REPO = "foo-repo";
    private const string GITHUB_ORG = "foo-gh-org";
    private const string GITHUB_REPO = "gh-repo";
    private readonly string ADO_REPO_URL = $"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_git/{ADO_REPO}";
    private readonly string ADO_TOKEN = Guid.NewGuid().ToString();
    private readonly string GITHUB_ORG_ID = Guid.NewGuid().ToString();
    private readonly string MIGRATION_SOURCE_ID = Guid.NewGuid().ToString();
    private readonly string MIGRATION_ID = Guid.NewGuid().ToString();
    private readonly string GITHUB_TOKEN = Guid.NewGuid().ToString();

    public MigrateRepoCommandHandlerTests()
    {
        _handler = new MigrateRepoCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object, _mockEnvironmentVariableProvider.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        // Arrange
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(GITHUB_ORG_ID, null).Result).Returns(MIGRATION_SOURCE_ID);
        _mockGithubApi
            .Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                ADO_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                ADO_TOKEN,
                GITHUB_TOKEN,
                null,
                null,
                false,
                false).Result)
            .Returns(MIGRATION_ID);
        _mockGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));

        _mockEnvironmentVariableProvider
            .Setup(m => m.GithubPersonalAccessToken())
            .Returns(GITHUB_TOKEN);
        _mockEnvironmentVariableProvider
            .Setup(m => m.AdoPersonalAccessToken())
            .Returns(ADO_TOKEN);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
        {
            "Migrating Repo...",
            $"ADO ORG: {ADO_ORG}",
            $"ADO TEAM PROJECT: {ADO_TEAM_PROJECT}",
            $"ADO REPO: {ADO_REPO}",
            $"GITHUB ORG: {GITHUB_ORG}",
            $"GITHUB REPO: {GITHUB_REPO}",
            $"A repository migration (ID: {MIGRATION_ID}) was successfully queued."
        };

        // Act
        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Wait = false,
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.GetOrganizationId(GITHUB_ORG));
        _mockGithubApi.Verify(m => m.CreateAdoMigrationSource(GITHUB_ORG_ID, null));
        _mockGithubApi.Verify(m => m.StartMigration(MIGRATION_SOURCE_ID, ADO_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, ADO_TOKEN, GITHUB_TOKEN, null, null, false, false));

        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Skip_Migration_If_Target_Repo_Exists()
    {
        // Arrange
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(GITHUB_ORG_ID, null).Result).Returns(MIGRATION_SOURCE_ID);
        _mockGithubApi
            .Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                ADO_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                ADO_TOKEN,
                GITHUB_TOKEN,
                null,
                null,
                false,
                false).Result)
            .Throws(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

        _mockEnvironmentVariableProvider
            .Setup(m => m.GithubPersonalAccessToken())
            .Returns(GITHUB_TOKEN);
        _mockEnvironmentVariableProvider
            .Setup(m => m.AdoPersonalAccessToken())
            .Returns(ADO_TOKEN);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = $"The Org '{GITHUB_ORG}' already contains a repository with the name '{GITHUB_REPO}'. No operation will be performed";

        // Act
        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Wait = false,
        };
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
        actualLogOutput.Should().Contain(expectedLogOutput);
    }

    [Fact]
    public async Task Happy_Path_With_Wait()
    {
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(GITHUB_ORG_ID, null).Result).Returns(MIGRATION_SOURCE_ID);
        _mockGithubApi
            .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    ADO_REPO_URL,
                    GITHUB_ORG_ID,
                    GITHUB_REPO,
                    ADO_TOKEN,
                    GITHUB_TOKEN,
                    null,
                    null,
                    false,
                    false).Result)
            .Returns(MIGRATION_ID);
        _mockGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));

        _mockEnvironmentVariableProvider
            .Setup(m => m.GithubPersonalAccessToken())
            .Returns(GITHUB_TOKEN);
        _mockEnvironmentVariableProvider
            .Setup(m => m.AdoPersonalAccessToken())
            .Returns(ADO_TOKEN);

        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Wait = true,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
    }

    [Fact]
    public async Task It_Falls_Back_To_Ado_And_Github_Pats_From_Environment_When_Not_Provided()
    {
        _mockGithubApi.Setup(x => x.GetRepos(GITHUB_ORG).Result).Returns(new List<string>());
        _mockGithubApi.Setup(x => x.GetMigration(It.IsAny<string>()).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));

        _mockEnvironmentVariableProvider
            .Setup(m => m.GithubPersonalAccessToken())
            .Returns(GITHUB_TOKEN);
        _mockEnvironmentVariableProvider
            .Setup(m => m.AdoPersonalAccessToken())
            .Returns(ADO_TOKEN);

        var args = new MigrateRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Wait = true,
        };

        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.StartMigration(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), ADO_TOKEN, GITHUB_TOKEN, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()));
        _mockEnvironmentVariableProvider.Verify(m => m.AdoPersonalAccessToken());
        _mockEnvironmentVariableProvider.Verify(m => m.GithubPersonalAccessToken());
    }
}
