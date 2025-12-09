using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommandHandlerTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<IVersionProvider> _mockVersionProvider = new();
        private readonly Mock<GhesVersionChecker> _mockGhesVersionCheckerService = TestHelpers.CreateMock<GhesVersionChecker>();

        private readonly GenerateScriptCommandHandler _handler;

        private const string SOURCE_ORG = "FOO-SOURCE-ORG";
        private const string TARGET_ORG = "FOO-TARGET-ORG";
        private const string REPO = "REPO";
        private const string AWS_BUCKET_NAME = "AWS_BUCKET_NAME";
        private const string AWS_REGION = "AWS_REGION";
        private const string UPLOADS_URL = "UPLOADS-URL";
        private string _script;

        public GenerateScriptCommandHandlerTests()
        {
            _handler = new GenerateScriptCommandHandler(
                _mockOctoLogger.Object,
                _mockGithubApi.Object,
                _mockVersionProvider.Object,
                _mockGhesVersionCheckerService.Object
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
        public async Task Sequential_Github_No_Data()
        {
            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().BeNull();
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()));
        }

        [Fact]
        public async Task Parallel_Github_No_Data()
        {
            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _handler.Handle(args);

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
                .ReturnsAsync(new[] { (REPO, "private") });

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().StartWith("#!/usr/bin/env pwsh");
        }

        [Fact]
        public async Task Parallel_Github_StartsWithShebang()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")

            };
            await _handler.Handle(args);

            // Assert
            _script.Should().StartWith("#!/usr/bin/env pwsh");
        }

        [Fact]
        public async Task Sequential_Github_Single_Repo()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_Github_Single_Repo_With_TargetApiUrl()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            var targetApiUrl = "https://foo.com/api/v3";
            var expected = $"Exec {{ gh gei migrate-repo --target-api-url \"{targetApiUrl}\" --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                TargetApiUrl = targetApiUrl
            };
            await _handler.Handle(args);

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
                .ReturnsAsync(new[] { (repo1, "private"), (repo2, "internal"), (repo3, "public") });

            var expected = new StringBuilder();
            expected.AppendLine($"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" --target-repo-visibility private }}");
            expected.AppendLine($"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" --target-repo-visibility internal }}");
            expected.Append($"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo3}\" --target-repo-visibility public }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Sequential_Github_Ghes_Repo()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                Sequential = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 21);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Parallel_Github_Multiple_Repos()
        {
            // Arrange
            const string repo1 = "FOO-REPO-1";
            const string repo2 = "FOO-REPO-2";

            _mockGithubApi.Setup(m => m.GetRepos(SOURCE_ORG)).ReturnsAsync(new[] { (repo1, "private"), (repo2, "public") });
            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
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
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{repo1}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" --queue-only --target-repo-visibility public }}");
            expected.AppendLine($"$RepoMigrations[\"{repo2}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{repo1}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo1}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{repo1}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{repo2}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo2}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{repo2}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
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
            await _handler.Handle(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
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
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
            expected.AppendLine(@"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
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
                GhesApiUrl = ghesApiUrl
            };
            await _handler.Handle(args);

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
                .ReturnsAsync(new[] { (repo1, "private"), (repo2, "private") });

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
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
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{repo1}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{repo2}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{repo1}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo1}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{repo1}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
            expected.AppendLine($"gh gei download-logs --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\"");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{repo2}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{repo2}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{repo2}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
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
            await _handler.Handle(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo_With_Download_Migration_Logs()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
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
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
            expected.AppendLine(@"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
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
                DownloadMigrationLogs = true
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo_No_Ssl()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
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
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}");
            expected.AppendLine(@"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}");
            expected.AppendLine();
            expected.AppendLine("$Succeeded = 0");
            expected.AppendLine("$Failed = 0");
            expected.AppendLine("$RepoMigrations = [ordered]@{}");
            expected.AppendLine();
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --no-ssl-verify --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
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
                NoSslVerify = true
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Parallel_Github_Ghes_Single_Repo_Keep_Archive()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(false);

            var expected = new StringBuilder();
            expected.AppendLine("#!/usr/bin/env pwsh");
            expected.AppendLine();
            expected.AppendLine("# =========== Created with CLI version 1.1.1 ===========");
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
            expected.AppendLine($"# =========== Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine("# === Queuing repo migrations ===");
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --keep-archive --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.AppendLine();
            expected.AppendLine();
            expected.AppendLine($"# =========== Waiting for all migrations to finish for Organization: {SOURCE_ORG} ===========");
            expected.AppendLine();
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");
            expected.AppendLine($"if ($RepoMigrations[\"{REPO}\"] -and $lastexitcode -eq 0) {{ $Succeeded++ }} else {{ $Failed++ }}");
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
                KeepArchive = true
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Adds_Skip_Releases_To_Migrate_Repo_Command_When_Provided_In_Sequential_Script()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --skip-releases --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                SkipReleases = true
            };
            await _handler.Handle(args);

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
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --queue-only --skip-releases --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.Append($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                SkipReleases = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 19, 7);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task It_Adds_Lock_Source_Repo_To_Migrate_Repo_Command_When_Provided_In_Sequential_Script()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --lock-source-repo --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                LockSourceRepo = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Parallel_It_Adds_Lock_Source_Repo_To_Migrate_Repo_Command_When_Provided_In_Parallel_Script()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --queue-only --lock-source-repo --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.Append($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                LockSourceRepo = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 19, 7);

            // Assert
            _script.Should().Be(expected.ToString());
        }

        [Fact]
        public async Task Sequential_Github_Contains_Cli_Version()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");

            const string expectedCliVersionComment = "# =========== Created with CLI version 1.1.1 ===========";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().Contain(expectedCliVersionComment);
        }

        [Fact]
        public async Task Parallel_Github_Contains_Cli_Version()
        {
            // Arrange
            _mockGithubApi.Setup(m => m.GetRepos(SOURCE_ORG)).ReturnsAsync(new[] { (REPO, "private") });
            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");

            const string expectedCliVersionComment = "# =========== Created with CLI version 1.1.1 ===========";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output")
            };
            await _handler.Handle(args);

            // Assert
            _script.Should().Contain(expectedCliVersionComment);
        }

        [Fact]
        public async Task Sequential_Ghes_Single_Repo_Aws_S3()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --aws-bucket-name \"{AWS_BUCKET_NAME}\" --aws-region \"{AWS_REGION}\" --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsRegion = AWS_REGION,
                Sequential = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 27, 0);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_Ghes_Single_Repo_Keep_Archive()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{ghesApiUrl}\" --aws-bucket-name \"{AWS_BUCKET_NAME}\" --aws-region \"{AWS_REGION}\" --keep-archive --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsRegion = AWS_REGION,
                Sequential = true,
                KeepArchive = true
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 27, 0);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Validates_Env_Vars()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = @"
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}

if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";

            expected = TrimNonExecutableLines(expected, 0, 0);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                Sequential = true,
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 0, 0);

            // Assert
            _script.Should().Contain(expected);
        }

        [Fact]
        public async Task Validates_Env_Vars_AWS()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = @"
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}

if (-not $env:AWS_ACCESS_KEY_ID) {
    Write-Error ""AWS_ACCESS_KEY_ID environment variable must be set to a valid AWS Access Key ID that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_ACCESS_KEY_ID environment variable is set and will be used to upload the migration archive to AWS S3.""
}

if (-not $env:AWS_SECRET_ACCESS_KEY) {
    Write-Error ""AWS_SECRET_ACCESS_KEY environment variable must be set to a valid AWS Secret Access Key that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_SECRET_ACCESS_KEY environment variable is set and will be used to upload the migration archive to AWS S3.""
}";

            expected = TrimNonExecutableLines(expected, 0, 0);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GhesApiUrl = ghesApiUrl,
                Output = new FileInfo("unit-test-output"),
                AwsBucketName = AWS_BUCKET_NAME,
                Sequential = true,
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 0, 0);

            // Assert
            _script.Should().Contain(expected);
        }

        [Fact]
        public async Task Validates_Env_Vars_AZURE_STORAGE_CONNECTION_STRING_Not_Validated_When_Aws()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(true);

            var expected = @"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";

            expected = TrimNonExecutableLines(expected, 0, 0);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                AwsBucketName = AWS_BUCKET_NAME,
                GhesApiUrl = ghesApiUrl,
                Sequential = true,
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 0, 0);

            // Assert
            _script.Should().NotContain(expected);
        }

        [Fact]
        public async Task Sequential_Github_Single_Repo_With_TargetUploadsUrl()
        {
            // Arrange
            var GHES_API_URL = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = $"Exec {{ gh gei migrate-repo --target-uploads-url \"{UPLOADS_URL}\" --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{GHES_API_URL}\" --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                TargetUploadsUrl = UPLOADS_URL,
                GhesApiUrl = GHES_API_URL,
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Sequential_Github_Single_Repo_With_UseGithubStorage()
        {
            // Arrange
            var GHES_API_URL = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"{GHES_API_URL}\" --use-github-storage --target-repo-visibility private }}";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                Sequential = true,
                UseGithubStorage = true,
                GhesApiUrl = GHES_API_URL,
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script);

            // Assert
            _script.Should().Be(expected);
        }

        [Fact]
        public async Task Parallel_Github_Single_Repo_With_UseGithubStorage()
        {
            // Arrange
            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });

            var expected = new StringBuilder();
            expected.AppendLine($"$MigrationID = ExecAndGetMigrationID {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{REPO}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{REPO}\" --ghes-api-url \"https://foo.com/api/v3\" --use-github-storage --queue-only --target-repo-visibility private }}");
            expected.AppendLine($"$RepoMigrations[\"{REPO}\"] = $MigrationID");
            expected.Append($"if ($RepoMigrations[\"{REPO}\"]) {{ gh gei wait-for-migration --migration-id $RepoMigrations[\"{REPO}\"] }}");

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                UseGithubStorage = true,
                GhesApiUrl = "https://foo.com/api/v3",
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 19, 7);

            // Assert
            _script.Should().Be(expected.ToString());
        }



        [Fact]
        public async Task Validates_Env_Vars_Blob_Storage_Not_Validated_When_GHES_3_8()
        {
            // Arrange
            const string ghesApiUrl = "https://foo.com/api/v3";

            _mockGithubApi
                .Setup(m => m.GetRepos(SOURCE_ORG))
                .ReturnsAsync(new[] { (REPO, "private") });
            _mockGhesVersionCheckerService.Setup(m => m.AreBlobCredentialsRequired(ghesApiUrl)).ReturnsAsync(false);

            var expected = @"
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}";
            var notExpected = @"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";

            expected = TrimNonExecutableLines(expected, 0, 0);

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                Output = new FileInfo("unit-test-output"),
                GhesApiUrl = ghesApiUrl,
                Sequential = true,
            };
            await _handler.Handle(args);

            _script = TrimNonExecutableLines(_script, 0, 0);

            // Assert
            _script.Should().Contain(expected);
            _script.Should().NotContain(notExpected);
        }

        private string TrimNonExecutableLines(string script, int skipFirst = 15, int skipLast = 0)
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
