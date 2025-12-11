using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands.GenerateScript;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.GenerateScript;

public class GenerateScriptCommandHandlerTests
{
    private const string ADO_ORG = "ADO_ORG";
    private const string ADO_TEAM_PROJECT = "ADO_TEAM_PROJECT";
    private const string FOO_REPO = "FOO_REPO";
    private const string FOO_PIPELINE = "FOO_PIPELINE";
    private const string BAR_REPO = "BAR_REPO";
    private const string BAR_PIPELINE = "BAR_PIPELINE";
    private const string APP_ID = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1";
    private const string GITHUB_ORG = "GITHUB_ORG";
    private const string ADO_SERVER_URL = "http://ado.contoso.com";

    private readonly IEnumerable<string> ADO_ORGS = [ADO_ORG];
    private readonly IEnumerable<string> ADO_TEAM_PROJECTS = [ADO_TEAM_PROJECT];
    private IEnumerable<AdoRepository> ADO_REPOS = [new() { Name = FOO_REPO }];
    private IEnumerable<string> ADO_PIPELINES = [FOO_PIPELINE];
    private readonly IEnumerable<string> EMPTY_PIPELINES = [];

    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<AdoInspectorService> _mockAdoInspector = TestHelpers.CreateMock<AdoInspectorService>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private string _scriptOutput;
    private readonly GenerateScriptCommandHandler _handler;

    public GenerateScriptCommandHandlerTests()
    {
        var mockVersionProvider = new Mock<IVersionProvider>();
        mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");

        _handler = new GenerateScriptCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, mockVersionProvider.Object, _mockAdoInspector.Object)
        {
            WriteToFile = (_, contents) =>
            {
                _scriptOutput = contents;
                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task SequentialScript_StartsWith_Shebang()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            AdoOrg = ADO_ORG,
            GithubOrg = GITHUB_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output")
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().StartWith("#!/usr/bin/env pwsh");
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_No_Options()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output")
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);
        var expected = $"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}";

        // Assert
        _scriptOutput.Should().Be(expected);
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_With_TargetApiUrl()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        var targetApiUrl = "https://foo.com/api/v3";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            TargetApiUrl = targetApiUrl
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);
        var expected = $"Exec {{ gh ado2gh migrate-repo --target-api-url \"{targetApiUrl}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}";

        // Assert
        _scriptOutput.Should().Be(expected);
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_AdoServer()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            AdoServerUrl = ADO_SERVER_URL,
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);
        var expected = $"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private --ado-server-url \"{ADO_SERVER_URL}\" }}";

        // Assert
        _scriptOutput.Should().Be(expected);
    }

    [Fact]
    public async Task SequentialScript_With_RepoList()
    {
        // Arrange
        var repoList = new FileInfo("repos.csv");

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            RepoList = repoList
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);
        var expected = $"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}";

        // Assert
        _scriptOutput.Should().Be(expected);
        _mockAdoInspector.Verify(m => m.LoadReposCsv(repoList.FullName));
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_All_Options()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(EMPTY_PIPELINES);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.Append($"Exec {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            All = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task Replaces_Invalid_Chars_With_Dashes()
    {
        // Arrange
        var adoTeamProject = "Parts Unlimited";
        var cleanedAdoTeamProject = "Parts-Unlimited";
        var adoTeamProjects = new List<string>() { adoTeamProject };
        var adoRepo = "Some Repo";
        var adoRepos = new List<AdoRepository> { new() { Name = adoRepo } };
        var expectedGithubRepoName = "Parts-Unlimited-Some-Repo";

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(adoTeamProjects);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, adoTeamProject)).ReturnsAsync(adoRepos);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, adoTeamProject, adoRepo)).ReturnsAsync(EMPTY_PIPELINES);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{cleanedAdoTeamProject}-Maintainers\" --idp-group \"{cleanedAdoTeamProject}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{cleanedAdoTeamProject}-Admins\" --idp-group \"{cleanedAdoTeamProject}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{expectedGithubRepoName}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{adoTeamProject}\" --ado-repo \"{adoRepo}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{expectedGithubRepoName}\" --team \"{cleanedAdoTeamProject}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{expectedGithubRepoName}\" --team \"{cleanedAdoTeamProject}-Admins\" --role \"admin\" }}");
        expected.Append($"Exec {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{expectedGithubRepoName}\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            All = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_No_Options_With_Download_Migration_Logs()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            DownloadMigrationLogs = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);
        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.Append($"Exec {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Skips_Team_Project_With_No_Repos()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(0);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output")
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().BeNull();
        _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_Two_Pipelines_All_Options()
    {
        // Arrange
        ADO_PIPELINES = [FOO_PIPELINE, BAR_PIPELINE];

        _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoApi.Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, ADO_TEAM_PROJECTS)).ReturnsAsync(APP_ID);

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(ADO_PIPELINES);


        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
        expected.Append($"Exec {{ gh ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{BAR_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            All = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Single_Repo_Two_Pipelines_No_Service_Connection_All_Options()
    {
        // Arrange
        ADO_PIPELINES = [FOO_PIPELINE, BAR_PIPELINE];

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(ADO_PIPELINES);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.Append($"Exec {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            All = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Create_Teams_Option_Should_Generate_Create_Team_And_Add_Teams_To_Repos_Scripts()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.Append($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            CreateTeams = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Link_Idp_Groups_Option_Should_Generate_Create_Teams_Scripts_With_Idp_Groups()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.Append($"Exec {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            LinkIdpGroups = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Lock_Ado_Repo_Option_Should_Generate_Lock_Ado_Repo_Script()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.Append($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            LockAdoRepos = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Disable_Ado_Repo_Option_Should_Generate_Disable_Ado_Repo_Script()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.Append($"Exec {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            DisableAdoRepos = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Contain(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_Rewire_Pipelines_Option_Should_Generate_Share_Service_Connection_And_Rewire_Pipeline_Scripts()
    {
        // Arrange
        _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoApi
            .Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, ADO_TEAM_PROJECTS))
            .ReturnsAsync(APP_ID);

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(ADO_PIPELINES);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.Append($"Exec {{ gh ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            RewirePipelines = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Contain(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_StartsWith_Shebang()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            AdoOrg = ADO_ORG,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo("unit-test-output")
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().StartWith("#!/usr/bin/env pwsh");
    }

    [Fact]
    public async Task ParallelScript_Single_Repo_No_Options()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine("#!/usr/bin/env pwsh");
        expected.AppendLine();
        expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
        expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
        expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
        expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
        expected.AppendLine(@"
if (-not $env:ADO_PAT) {
    Write-Error ""ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#personal-access-tokens-for-azure-devops""
    exit 1
} else {
    Write-Host ""ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps.""
}

if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
        expected.AppendLine();
        expected.AppendLine("$Succeeded = 0");
        expected.AppendLine("$Failed = 0");
        expected.AppendLine("$RepoMigrations = [ordered]@{}");
        expected.AppendLine();
        expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
        expected.AppendLine();
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine();
        expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    $Succeeded++");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.AppendLine("}");
        expected.AppendLine();
        expected.AppendLine("Write-Host =============== Summary ===============");
        expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
        expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
        expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
        expected.AppendLine();
        expected.AppendLine();

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output")
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Single_Repo_No_Options_With_Download_Migration_Logs()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine("#!/usr/bin/env pwsh");
        expected.AppendLine();
        expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
        expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
        expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
        expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
        expected.AppendLine(@"
if (-not $env:ADO_PAT) {
    Write-Error ""ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#personal-access-tokens-for-azure-devops""
    exit 1
} else {
    Write-Host ""ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps.""
}

if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
        expected.AppendLine();
        expected.AppendLine("$Succeeded = 0");
        expected.AppendLine("$Failed = 0");
        expected.AppendLine("$RepoMigrations = [ordered]@{}");
        expected.AppendLine();
        expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
        expected.AppendLine();
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine();
        expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.AppendLine("}");
        expected.AppendLine();
        expected.AppendLine("Write-Host =============== Summary ===============");
        expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
        expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
        expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
        expected.AppendLine();
        expected.AppendLine();

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            DownloadMigrationLogs = true
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Skips_Team_Project_With_No_Repos()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(0);

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output")
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().BeNull();
        _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
    }

    [Fact]
    public async Task ParallelScript_Two_Repos_Two_Pipelines_All_Options()
    {
        // Arrange
        ADO_REPOS = [new() { Name = FOO_REPO }, new() { Name = BAR_REPO }];

        _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoApi.Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, ADO_TEAM_PROJECTS)).ReturnsAsync(APP_ID);

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(2);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(new[] { FOO_PIPELINE });
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO)).ReturnsAsync(new[] { BAR_PIPELINE });

        var expected = new StringBuilder();
        expected.AppendLine("#!/usr/bin/env pwsh");
        expected.AppendLine();
        expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
        expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
        expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
        expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
        expected.AppendLine(@"
if (-not $env:ADO_PAT) {
    Write-Error ""ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#personal-access-tokens-for-azure-devops""
    exit 1
} else {
    Write-Host ""ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps.""
}

if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
        expected.AppendLine();
        expected.AppendLine("$Succeeded = 0");
        expected.AppendLine("$Failed = 0");
        expected.AppendLine("$RepoMigrations = [ordered]@{}");
        expected.AppendLine();
        expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine();
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine();
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"] = $MigrationID");
        expected.AppendLine();
        expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.AppendLine($"        {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
        expected.AppendLine($"        {{ gh ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.AppendLine("}");
        expected.AppendLine();
        expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {BAR_REPO}. Will then complete the below post migration steps. ===");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{BAR_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{BAR_REPO}\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.AppendLine($"        {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" }}");
        expected.AppendLine($"        {{ gh ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{BAR_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{BAR_REPO}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.AppendLine("}");
        expected.AppendLine();
        expected.AppendLine("Write-Host =============== Summary ===============");
        expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
        expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
        expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
        expected.AppendLine();
        expected.AppendLine();

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            All = true
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Single_Repo_No_Service_Connection_All_Options()
    {
        // Arrange
        ADO_PIPELINES = [FOO_PIPELINE, BAR_PIPELINE];

        _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(ADO_PIPELINES);

        var expected = new StringBuilder();
        expected.AppendLine("#!/usr/bin/env pwsh");
        expected.AppendLine();
        expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
        expected.AppendLine(@"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}");
        expected.AppendLine(@"
function ExecAndGetMigrationID {
    param (
        [scriptblock]$ScriptBlock
    )
    $MigrationID = & @ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
        expected.AppendLine(@"
function ExecBatch {
    param (
        [scriptblock[]]$ScriptBlocks
    )
    $Global:LastBatchFailures = 0
    foreach ($ScriptBlock in $ScriptBlocks)
    {
        & @ScriptBlock
        if ($lastexitcode -ne 0) {
            $Global:LastBatchFailures++
        }
    }
}");
        expected.AppendLine(@"
if (-not $env:ADO_PAT) {
    Write-Error ""ADO_PAT environment variable must be set to a valid Azure DevOps Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#personal-access-tokens-for-azure-devops""
    exit 1
} else {
    Write-Host ""ADO_PAT environment variable is set and will be used to authenticate to Azure DevOps.""
}

if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
        expected.AppendLine();
        expected.AppendLine("$Succeeded = 0");
        expected.AppendLine("$Failed = 0");
        expected.AppendLine("$RepoMigrations = [ordered]@{}");
        expected.AppendLine();
        expected.AppendLine($"# =========== Queueing migration for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos");
        expected.AppendLine();
        expected.AppendLine($"# === Queueing repo migrations for Team Project: {ADO_ORG}/{ADO_TEAM_PROJECT} ===");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine();
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine();
        expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {ADO_ORG} ===========");
        expected.AppendLine();
        expected.AppendLine($"# === Waiting for repo migration to finish for Team Project: {ADO_TEAM_PROJECT} and Repo: {FOO_REPO}. Will then complete the below post migration steps. ===");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.AppendLine($"        {{ gh ado2gh download-logs --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.AppendLine("}");
        expected.AppendLine();
        expected.AppendLine("Write-Host =============== Summary ===============");
        expected.AppendLine("Write-Host Total number of successful migrations: $Succeeded");
        expected.AppendLine("Write-Host Total number of failed migrations: $Failed");
        expected.AppendLine(@"
if ($Failed -ne 0) {
    exit 1
}");
        expected.AppendLine();
        expected.AppendLine();

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            All = true
        };
        await _handler.Handle(args);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Create_Teams_Option_Should_Generate_Create_Teams_And_Add_Teams_To_Repos_Scripts()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.Append('}');

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            CreateTeams = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput, 47, 6);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Link_Idp_Groups_Option_Should_Generate_Create_Teams_Scripts_With_Idp_Groups()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" --idp-group \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" --idp-group \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.AppendLine($"        {{ gh ado2gh add-team-to-repo --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.Append('}');

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            LinkIdpGroups = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput, 47, 6);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Lock_Ado_Repo_Option_Should_Generate_Lock_Ado_Repo_Script()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh lock-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    $Succeeded++");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.Append('}');

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            LockAdoRepos = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput, 47, 6);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Disable_Ado_Repo_Option_Should_Generate_Disable_Ado_Repo_Script()
    {
        // Arrange
        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh disable-ado-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.Append('}');

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            DisableAdoRepos = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput, 47, 6);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task ParallelScript_Rewire_Pipelines_Option_Should_Generate_Share_Service_Connection_And_Rewire_Pipeline_Scripts()
    {
        // Arrange
        _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoApi.Setup(m => m.GetGithubAppId(ADO_ORG, GITHUB_ORG, new[] { ADO_TEAM_PROJECT })).ReturnsAsync(APP_ID);

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
        _mockAdoInspector.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(ADO_PIPELINES);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh share-service-connection --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh ado2gh migrate-repo --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --queue-only --target-repo-visibility private }}");
        expected.AppendLine($"$RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"] = $MigrationID");
        expected.AppendLine("$CanExecuteBatch = $false");
        expected.AppendLine($"if ($null -ne $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]) {{");
        expected.AppendLine($"    gh ado2gh wait-for-migration --migration-id $RepoMigrations[\"{ADO_ORG}/{ADO_TEAM_PROJECT}-{FOO_REPO}\"]");
        expected.AppendLine("    $CanExecuteBatch = ($lastexitcode -eq 0)");
        expected.AppendLine("}");
        expected.AppendLine("if ($CanExecuteBatch) {");
        expected.AppendLine("    ExecBatch @(");
        expected.AppendLine($"        {{ gh ado2gh rewire-pipeline --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-pipeline \"{FOO_PIPELINE}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --service-connection-id \"{APP_ID}\" }}");
        expected.AppendLine("    )");
        expected.AppendLine("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }");
        expected.AppendLine("} else {");
        expected.AppendLine("    $Failed++");
        expected.Append('}');

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Output = new FileInfo("unit-test-output"),
            RewirePipelines = true
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput, 47, 6);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    [Fact]
    public async Task SequentialScript_CreateTeams_With_TargetApiUrl_Should_Include_TargetApiUrl_In_AddTeamToRepo_Commands()
    {
        // Arrange
        const string TARGET_API_URL = "https://example.com/api/v3";

        _mockAdoInspector.Setup(m => m.GetRepoCount()).ReturnsAsync(1);
        _mockAdoInspector.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
        _mockAdoInspector.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
        _mockAdoInspector.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);

        var expected = new StringBuilder();
        expected.AppendLine($"Exec {{ gh ado2gh create-team --target-api-url \"{TARGET_API_URL}\" --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Maintainers\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh create-team --target-api-url \"{TARGET_API_URL}\" --github-org \"{GITHUB_ORG}\" --team-name \"{ADO_TEAM_PROJECT}-Admins\" }}");
        expected.AppendLine($"Exec {{ gh ado2gh migrate-repo --target-api-url \"{TARGET_API_URL}\" --ado-org \"{ADO_ORG}\" --ado-team-project \"{ADO_TEAM_PROJECT}\" --ado-repo \"{FOO_REPO}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --target-repo-visibility private }}");
        expected.AppendLine($"Exec {{ gh ado2gh add-team-to-repo --target-api-url \"{TARGET_API_URL}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Maintainers\" --role \"maintain\" }}");
        expected.Append($"Exec {{ gh ado2gh add-team-to-repo --target-api-url \"{TARGET_API_URL}\" --github-org \"{GITHUB_ORG}\" --github-repo \"{ADO_TEAM_PROJECT}-{FOO_REPO}\" --team \"{ADO_TEAM_PROJECT}-Admins\" --role \"admin\" }}");

        // Act
        var args = new GenerateScriptCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            AdoOrg = ADO_ORG,
            Sequential = true,
            Output = new FileInfo("unit-test-output"),
            CreateTeams = true,
            TargetApiUrl = TARGET_API_URL
        };
        await _handler.Handle(args);

        _scriptOutput = TrimNonExecutableLines(_scriptOutput);

        // Assert
        _scriptOutput.Should().Be(expected.ToString());
    }

    private string TrimNonExecutableLines(string script, int skipFirst = 21, int skipLast = 0)
    {
        var lines = script.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

        lines = lines
            .Where(x => x.HasValue())
            .Where(x => !x.Trim().StartsWith("#"))
            .Skip(skipFirst)
            .SkipLast(skipLast);

        return string.Join(Environment.NewLine, lines);
    }
}
