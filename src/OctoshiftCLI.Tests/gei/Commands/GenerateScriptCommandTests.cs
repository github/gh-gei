using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GenerateScriptCommandTests
    {
        private const string SOURCE_ORG = "foo-source-org";
        private const string TARGET_ORG = "foo-target-org";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null, null, null);

            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(14);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-releases", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void Github_No_Data()
        {
            var command = new GenerateScriptCommand(null, null, null, null);
            var script = command.GenerateSequentialGithubScript(null, "foo-source", "foo-target", "", "", false, false);

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Github_Sequential_StartsWithShebang()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, false);

            script.Should().StartWith("#!/usr/bin/pwsh");

        }

        [Fact]
        public void Github_Single_Repo()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --wait }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_Multiple_Repos()
        {
            var repo1 = "foo-repo-1";
            var repo2 = "foo-repo-2";
            var repo3 = "foo-repo-3";
            var repos = new List<string>() { repo1, repo2, repo3 };

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo3}\" --wait }}";

            script.Should().Be(expected);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects()
        {
            var org = "foo-org";
            var teamProject = string.Empty;
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var result = await command.GetAdoRepos(mockAdo.Object, org, teamProject);

            Assert.Single(result[teamProject1]);
            Assert.Single(result[teamProject2]);
            Assert.Contains(result[teamProject1], x => x == repo1);
            Assert.Contains(result[teamProject2], x => x == repo2);
        }

        [Fact]
        public async Task GetRepos_Two_Repos_Two_Team_Projects_With_Team_Project_Supplied()
        {
            var org = "foo-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjectArg = teamProject1;
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var result = await command.GetAdoRepos(mockAdo.Object, org, teamProjectArg);

            Assert.Single(result[teamProjectArg]);
            Assert.False(result.ContainsKey(teamProject2));
            Assert.Contains(result[teamProjectArg], x => x == repo1);
        }

        [Fact]
        public async Task GetRepos_With_Team_Project_Supplied_Does_Not_Exist()
        {
            var org = "foo-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjectArg = "foo-tp3";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var repo1 = "foo-repo1";
            var repo2 = "foo-repo2";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();

            mockAdo.Setup(x => x.GetTeamProjects(org).Result).Returns(teamProjects);
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject1).Result).Returns(new List<string>() { repo1 });
            mockAdo.Setup(x => x.GetEnabledRepos(org, teamProject2).Result).Returns(new List<string>() { repo2 });

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var result = await command.GetAdoRepos(mockAdo.Object, org, teamProjectArg);

            Assert.Empty(result);
        }

        [Fact]
        public void Github_GHES_Repo()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };
            var ghesApiUrl = "https://api.foo.com";
            var azureStorageConnectionString = "foo-storage-connection-string";

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialGithubScript(repos, SOURCE_ORG, TARGET_ORG, ghesApiUrl, azureStorageConnectionString, false, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --wait }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_GHES_Repo_No_Ssl()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };
            var ghesApiUrl = "https://api.foo.com";
            var azureStorageConnectionString = "foo-storage-connection-string";

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialGithubScript(repos, SOURCE_ORG, TARGET_ORG, ghesApiUrl, azureStorageConnectionString, true, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --no-ssl-verify --wait }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_No_Data()
        {
            var command = new GenerateScriptCommand(null, null, null, null);
            var script = command.GenerateSequentialAdoScript(null, "foo-source", "foo-target");

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Ado_Single_Repo()
        {
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";
            var repos = new Dictionary<string, IEnumerable<string>>() { { adoTeamProject, new List<string>() { repo } } };

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialAdoScript(repos, SOURCE_ORG, TARGET_ORG);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo}\" --wait }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_Multiple_Repos()
        {
            var adoTeamProject = "foo-team-project";
            var repo1 = "foo-repo-1";
            var repo2 = "foo-repo-2";
            var repo3 = "foo-repo-3";
            var repos = new Dictionary<string, IEnumerable<string>> { { adoTeamProject, new List<string>() { repo1, repo2, repo3 } } };

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialAdoScript(repos, SOURCE_ORG, TARGET_ORG);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" --wait }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo3}\" --wait }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void GenerateParallelAdoScript_No_Data()
        {
            // Arrange, Act
            var command = new GenerateScriptCommand(null, null, null, null);
            var script = command.GenerateParallelAdoScript(null, "foo-source", "foo-target");

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public void GenerateParallelAdoScript_Multiple_Repos()
        {
            // Arrange
            const string adoTeamProject = "foo-team-project";
            const string repo1 = "foo-repo-1";
            const string repo2 = "foo-repo-2";
            var repos = new Dictionary<string, IEnumerable<string>> { { adoTeamProject, new[] { repo1, repo2 } } };

            var expected = new StringBuilder();
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
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateParallelAdoScript(repos, SOURCE_ORG, TARGET_ORG);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public void GenerateParallelGithubScript_No_Data()
        {
            // Arrange, Act
            var command = new GenerateScriptCommand(null, null, null, null);
            var script = command.GenerateParallelGithubScript(null, "github-source", "github-target", "", "", false, false);

            // Assert
            script.Should().BeEmpty();
        }

        [Fact]
        public void GenerateParallelGithubScript_Multiple_Repos()
        {
            // Arrange
            const string repo1 = "foo-repo-1";
            const string repo2 = "foo-repo-2";
            var repos = new[] { repo1, repo2 };

            var expected = new StringBuilder();
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
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateParallelGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, false);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public void GenerateParallelGithubScript_Ghes_Single_Repo()
        {
            // Arrange
            const string ghesApiUrl = "https://api.foo.com";
            const string azureStorageConnectionString = "foo-storage-connection-string";
            const string repo = "foo-repo";
            var repos = new[] { repo };

            var expected = new StringBuilder();
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
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" }}");
            expected.AppendLine($"$RepoMigrations[\"{repo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo}\"]");
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
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateParallelGithubScript(repos, SOURCE_ORG, TARGET_ORG, ghesApiUrl, azureStorageConnectionString, false, false);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public void GenerateParallelGithubScript_Ghes_Single_Repo_No_Ssl()
        {
            // Arrange
            const string ghesApiUrl = "https://api.foo.com";
            const string azureStorageConnectionString = "foo-storage-connection-string";
            const string repo = "foo-repo";
            var repos = new[] { repo };

            var expected = new StringBuilder();
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
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --no-ssl-verify }}");
            expected.AppendLine($"$RepoMigrations[\"{repo}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo}\"]");
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
            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateParallelGithubScript(repos, SOURCE_ORG, TARGET_ORG, ghesApiUrl, azureStorageConnectionString, true, false);

            // Assert
            script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Uses_Github_Source_Pat_When_Provided()
        {
            // Arrange
            const string githubSourcePat = "github-source-pat";

            var mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), githubSourcePat))
                .Returns(mockSourceGithubApi.Object);

            var mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();

            // Act
            var command = new GenerateScriptCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockSourceGithubApiFactory.Object,
                mockAdoApiFactory.Object,
                mockEnvironmentVariableProvider.Object);
            await command.Invoke("githubSourceOrg", null, null, "githubTargetOrg", null, githubSourcePat: githubSourcePat);

            // Assert
            mockSourceGithubApiFactory.Verify(m => m.Create(null, githubSourcePat));
            mockEnvironmentVariableProvider.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Uses_Ado_Pat_When_Provided()
        {
            // Arrange
            const string adoPat = "ado-pat";

            var mockAdoApi = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdoApi.Object);

            var mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory
                .Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockSourceGithubApi.Object);

            var mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

            // Act
            var command = new GenerateScriptCommand(
                TestHelpers.CreateMock<OctoLogger>().Object,
                mockSourceGithubApiFactory.Object,
                mockAdoApiFactory.Object,
                mockEnvironmentVariableProvider.Object);
            await command.Invoke(null, "adoSourceOrg", null, "githubTargetOrg", null, adoPat: adoPat);

            // Assert
            mockAdoApiFactory.Verify(m => m.Create(adoPat));
            mockEnvironmentVariableProvider.VerifyNoOtherCalls();
        }

        [Fact]
        public void It_Adds_Skip_Releases_To_Migrate_Repo_Command_When_Provided()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateSequentialGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, true);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --wait --skip-releases }}";

            script.Should().Be(expected);
        }

        private string TrimNonExecutableLines(string script)
        {
            var lines = script.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !x.Trim().StartsWith("#"));
            // This skips the Exec function definition
            lines = lines.Skip(9);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
