using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ISourceGithubApiFactory> _mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<IVersionProvider> _mockVersionProvider = new Mock<IVersionProvider>();

        private readonly GenerateScriptCommand _command;

        private const string SOURCE_ORG = "FOO-SOURCE-ORG";
        private const string TARGET_ORG = "FOO-TARGET-ORG";
        private const string REPO = "REPO";
        private string _script;

        public GenerateScriptCommandTests()
        {
            _command = new GenerateScriptCommand(
                _mockOctoLogger.Object,
                _mockSourceGithubApiFactory.Object,
                _mockAdoApiFactory.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockVersionProvider.Object
                )
            {
                WriteToFile = (_, contents) =>
                {
                    _script = contents;
                    return Task.CompletedTask;
                }
            };
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("generate-script");
            _command.Options.Count.Should().Be(16);

            TestHelpers.VerifyCommandOption(_command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-server-url", false, true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-source-org", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(_command.Options, "skip-releases", false);
            TestHelpers.VerifyCommandOption(_command.Options, "download-migration-logs", false);
            TestHelpers.VerifyCommandOption(_command.Options, "output", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(_command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task AdoServer_Source_Without_SourceOrg_Provided_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _command.Invoke(new GenerateScriptCommandArgs
                {
                    AdoServerUrl = "https://ado.contoso.com",
                    GithubTargetOrg = TARGET_ORG
                }
                ))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Sequential_Github_No_Data()
        {
            // Arrange
            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().BeNull();
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
        }

        [Fact]
        public async Task Parallel_Github_No_Data()
        {
            // Arrange
            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().BeNull();
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
        }

        [Fact]
        public async Task Sequential_Github_StartsWithShebang()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().StartWith("#!/usr/bin/env pwsh");
        }

        [Fact]
        public async Task Parallel_Github_StartsWithShebang()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")

            };
            await _command.Invoke(args);

            // Assert
            _script.Should().StartWith("#!/usr/bin/env pwsh");
        }

        [Fact]
        public async Task Sequential_Github_Single_Repo()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --wait }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_Github_Multiple_Repos()
        {
            // Arrange
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";
            const string repo3 = "FOO-REPO-3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { repo1, repo2, repo3 });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" --wait }}");
            expected.AppendLine($"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" --wait }}");
            expected.Append($"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo3}\" --wait }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Invoke_Gets_All_Ado_Repos_For_Each_Team_Projects_When_Ado_Team_Project_Is_Not_Provided()
        {
            // Arrnage
            const string org = "foo-org";
            const string teamProject1 = "foo-tp1";
            const string teamProject2 = "foo-tp2";
            const string repo1 = "foo-repo1";
            const string repo2 = "foo-repo2";

            _mockAdoApi.Setup(x => x.GetTeamProjects(org)).ReturnsAsync(new[] { teamProject1, teamProject2 });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(org, teamProject1)).ReturnsAsync(new[] { new AdoRepository { Name = repo1 } });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(org, teamProject2)).ReturnsAsync(new[] { new AdoRepository { Name = repo2 } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = org,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().NotBeEmpty();
            _mockAdoApi.Verify(m => m.GetTeamProjects(org), Times.Once);
            _mockAdoApi.Verify(m => m.GetEnabledRepos(org, teamProject1), Times.Once);
            _mockAdoApi.Verify(m => m.GetEnabledRepos(org, teamProject2), Times.Once);
            _mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_Gets_All_Ado_Repos_For_Provided_Team_Project()
        {
            // Arrnage
            const string org = "FOO-ORG";
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string anotherTeamProject = "ANOTHER_TEAM_PROJECT";
            const string repo1 = "FOO-REPO1";
            const string repo2 = "FOO-REPO2";

            _mockAdoApi.Setup(x => x.GetTeamProjects(org)).ReturnsAsync(new[] { adoTeamProject, anotherTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(org, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = repo1 }, new AdoRepository { Name = repo2 } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = org,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().NotBeEmpty();
            _mockAdoApi.Verify(m => m.GetTeamProjects(org), Times.Once);
            _mockAdoApi.Verify(m => m.GetEnabledRepos(org, adoTeamProject), Times.Once);
            _mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Invoke_Gets_No_Repos_When_Provided_Team_Project_Does_Not_Exist()
        {
            // Arrnage
            const string org = "FOO-ORG";
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string anotherTeamProject = "ANOTHER_TEAM_PROJECT";

            _mockAdoApi.Setup(x => x.GetTeamProjects(org)).ReturnsAsync(new[] { adoTeamProject, anotherTeamProject });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = org,
                AdoTeamProject = "NOT_EXISTING_TEAM_PROJECT",
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().BeNull();
            _mockAdoApi.Verify(m => m.GetTeamProjects(org), Times.Once);
            _mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Sequential_Github_Ghes_Repo()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";
            const string azureStorageConnectionString = "FOO-STORAGE-CONNECTION-STRING";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(ghesApiUrl, It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --wait }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = azureStorageConnectionString,
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
            _mockOctoLogger.Verify(m => m.LogInformation("AZURE STORAGE CONNECTION STRING: ***"));
            _mockOctoLogger.Verify(m => m.LogInformation($"GHES API URL: {ghesApiUrl}"));
        }

        [Fact]
        public async Task Sequential_Github_Ghes_Repo_No_Ssl()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";
            const string azureStorageConnectionString = "foo-storage-connection-string";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(ghesApiUrl, It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --no-ssl-verify --wait }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = azureStorageConnectionString,
                Sequential = true,
                NoSslVerify = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
            _mockOctoLogger.Verify(m => m.LogInformation("SSL verification disabled"));
        }

        [Fact]
        public async Task Sequential_Ado_No_Data()
        {
            // Arrange
            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().BeNull();
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
        }

        [Fact]
        public async Task Parallel_Ado_No_Data()
        {
            // Arrange
            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().BeNull();
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
        }

        [Fact]
        public async Task Sequential_Ado_Single_Repo()
        {
            // Arrnage
            const string adoTeamProject = "ADO-TEAM-PROJECT";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = REPO } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{REPO}\" --wait }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_Ado_With_Spaces()
        {
            // Arrnage
            const string adoTeamProject = "ADO TEAM PROJECT";
            const string adoRepo = "SOME REPO";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = adoRepo } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{adoRepo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"ADO-TEAM-PROJECT-SOME-REPO\" --wait }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_AdoServer_Single_Repo()
        {
            // Arrnage
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string adoServerUrl = "https://ado.contoso.com";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = REPO } });

            _mockAdoApiFactory
                .Setup(m => m.Create(adoServerUrl, null))
                .Returns(_mockAdoApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --ado-server-url \"{adoServerUrl}\" --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{REPO}\" --wait }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoServerUrl = adoServerUrl,
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_Ado_Multiple_Repos()
        {
            // Arrnage
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";
            const string repo3 = "FOO-REPO-3";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[]
            {
                new AdoRepository { Name = repo1 },
                new AdoRepository { Name = repo2 },
                new AdoRepository { Name = repo3 }
            });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" --wait }}");
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" --wait }}");
            expected.Append($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo3}\" --wait }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Ado_Multiple_Repos()
        {
            // Arrange
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = repo1 }, new AdoRepository { Name = repo2 } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queuing repo migrations for Team Project: {SOURCE_ORG}/{adoTeamProject} ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoTeamProject}-{repo1}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoTeamProject}-{repo2}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Migration stauts for Team Project: {SOURCE_ORG}/{adoTeamProject} ===");
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{adoTeamProject}-{repo1}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{adoTeamProject}-{repo2}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine();
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
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Multiple_Repos()
        {
            // Arrange
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";

            _mockGithubApi.Setup(m => m.GetRepos(SOURCE_ORG)).ReturnsAsync(new[] { repo1, repo2 });
            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{repo1}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{repo2}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo1}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo2}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine();
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
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";
            const string azureStorageConnectionString = "FOO-STORAGE-CONNECTION-STRING";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(ghesApiUrl, It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine();
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
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = azureStorageConnectionString
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
            _mockOctoLogger.Verify(m => m.LogInformation("AZURE STORAGE CONNECTION STRING: ***"));
            _mockOctoLogger.Verify(m => m.LogInformation($"GHES API URL: {ghesApiUrl}"));
        }

        [Fact]
        public async Task Sequential_Ado_Single_Repo_With_Download_Migration_Logs()
        {
            // Arrange
            const string adoTeamProject = "ADO-TEAM-PROJECT";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, It.IsAny<string>())).ReturnsAsync(new[] { new AdoRepository { Name = REPO } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{REPO}\" --wait }}");
            expected.Append($"Exec {{ gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{REPO}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                DownloadMigrationLogs = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Sequential_AdoServer_Single_Repo_With_Download_Migration_Logs()
        {
            // Arrnage
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string adoServerUrl = "https://ado.contoso.com";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = REPO } });

            _mockAdoApiFactory
                .Setup(m => m.Create(adoServerUrl, null))
                .Returns(_mockAdoApi.Object);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-server-url \"{adoServerUrl}\" --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{REPO}\" --wait }}");
            expected.Append($"Exec {{ gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{REPO}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoServerUrl = adoServerUrl,
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                DownloadMigrationLogs = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Sequential_Ado_Multiple_Repos_With_Download_Migration_Logs()
        {
            // Arrange
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";
            const string repo3 = "FOO-REPO-3";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[]
            {
                new AdoRepository { Name = repo1},
                new AdoRepository { Name = repo2},
                new AdoRepository { Name = repo3}
            });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" --wait }}");
            expected.AppendLine($"Exec {{ gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" }}");
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" --wait }}");
            expected.AppendLine($"Exec {{ gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" }}");
            expected.AppendLine($"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo3}\" --wait }}");
            expected.Append($"Exec {{ gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo3}\" }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                DownloadMigrationLogs = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Ado_Multiple_Repos_With_Download_Migration_Logs()
        {
            // Arrange
            const string adoTeamProject = "ADO-TEAM-PROJECT";
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, It.IsAny<string>())).ReturnsAsync(new[] { new AdoRepository { Name = repo1 }, new AdoRepository { Name = repo2 } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Queuing repo migrations for Team Project: {SOURCE_ORG}/{adoTeamProject} ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoTeamProject}-{repo1}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{adoTeamProject}-{repo2}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"# === Migration stauts for Team Project: {SOURCE_ORG}/{adoTeamProject} ===");
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{adoTeamProject}-{repo1}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine($"gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\"");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{adoTeamProject}-{repo2}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine($"gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\"");
            expected.AppendLine();
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
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                DownloadMigrationLogs = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Multiple_Repos_With_Download_Migration_Logs()
        {
            // Arrange
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { repo1, repo2 });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{repo1}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{repo2}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo1}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine($"gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\"");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo2}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine($"gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\"");
            expected.AppendLine();
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
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                DownloadMigrationLogs = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo_With_Download_Migration_Logs()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";
            const string azureStorageConnectionString = "FOO-STORAGE-CONNECTION-STRING";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine($"gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\"");
            expected.AppendLine();
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
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = azureStorageConnectionString,
                DownloadMigrationLogs = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo_No_Ssl()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";
            const string azureStorageConnectionString = "FOO-STORAGE-CONNECTION-STRING";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(ghesApiUrl, It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1.1 ===========");
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
    $MigrationID = Exec $ScriptBlock | ForEach-Object {
        Write-Host $_
        $_
    } | Select-String -Pattern ""\(ID: (.+)\)"" | ForEach-Object { $_.matches.groups[1] }
    return $MigrationID
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --no-ssl-verify }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"]");
            expected.AppendLine("if ($lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }");
            expected.AppendLine();
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
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = azureStorageConnectionString,
                NoSslVerify = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Be(expected.ToString());
            _mockOctoLogger.Verify(m => m.LogInformation("SSL verification disabled"));
        }

        [Fact]
        public async Task It_Uses_Github_Source_Pat_When_Provided()
        {
            // Arrange
            const string githubSourcePat = "github-source-pat";

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), githubSourcePat))
                .Returns(_mockGithubApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubSourcePat = githubSourcePat
            };
            await _command.Invoke(args);

            // Assert
            _mockSourceGithubApiFactory.Verify(m => m.Create(null, githubSourcePat));
            _mockEnvironmentVariableProvider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Uses_Ado_Pat_When_Provided()
        {
            // Arrange
            const string adoPat = "ado-pat";

            _mockAdoApiFactory.Setup(m => m.Create(null, adoPat)).Returns(_mockAdoApi.Object);

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                AdoPat = adoPat
            };
            await _command.Invoke(args);

            // Assert
            _mockAdoApiFactory.Verify(m => m.Create(null, adoPat));
            _mockEnvironmentVariableProvider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Adds_Skip_Releases_To_Migrate_Repo_Command_When_Provided_In_Sequential_Script()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --wait --skip-releases }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                SkipReleases = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Parallel_It_Adds_Skip_Releases_To_Migrate_Repo_Command_When_Provided_In_Parallel_Script()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --skip-releases }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.Append($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"]");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                SkipReleases = true
            };
            await _command.Invoke(args);

            _script = TrimNonExecutableLines(_script, 22, 7);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Sequential_Github_Contains_Cli_Version()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { REPO });

            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            const string expectedCliVersionComment = "# =========== Created with CLI version 1.1.1.1 ===========";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Contain(expectedCliVersionComment);
        }

        [Fact]
        public async Task Parallel_Github_Contains_Cli_Version()
        {
            // Arrange
            _mockGithubApi.Setup(m => m.GetRepos(SOURCE_ORG)).ReturnsAsync(new[] { REPO });
            _mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockGithubApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            const string expectedCliVersionComment = "# =========== Created with CLI version 1.1.1.1 ===========";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Contain(expectedCliVersionComment);
        }

        [Fact]
        public async Task Sequential_Ado_Contains_Cli_Version()
        {
            // Arrnage
            const string adoTeamProject = "ADO-TEAM-PROJECT";

            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, adoTeamProject)).ReturnsAsync(new[] { new AdoRepository { Name = REPO } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            const string expectedCliVersionComment = "# =========== Created with CLI version 1.1.1.1 ===========";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Contain(expectedCliVersionComment);
        }

        [Fact]
        public async Task Parallel_Ado_Contains_Cli_Version()
        {
            const string adoTeamProject = "ADO-TEAM-PROJECT";

            // Arrange
            _mockAdoApi.Setup(x => x.GetTeamProjects(SOURCE_ORG)).ReturnsAsync(new[] { adoTeamProject });
            _mockAdoApi.Setup(x => x.GetEnabledRepos(SOURCE_ORG, It.IsAny<string>())).ReturnsAsync(new[] { new AdoRepository { Name = REPO } });

            _mockAdoApiFactory
                .Setup(m => m.Create(null, null))
                .Returns(_mockAdoApi.Object);

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");

            const string expectedCliVersionComment = "# =========== Created with CLI version 1.1.1.1 ===========";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _command.Invoke(args);

            // Assert
            _script.Should().Contain(expectedCliVersionComment);
        }

        private string TrimNonExecutableLines(string script, int skipFirst = 9, int skipLast = 0)
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
}
