using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
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
            command.Options.Count.Should().Be(9);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void Github_No_Data()
        {
            var command = new GenerateScriptCommand(null, null, null, null);
            var script = command.GenerateGithubScript(null, "foo-source", "foo-target", "", "", false, false);

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Github_Single_Repo()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_Multiple_Repos()
        {
            var repo1 = "foo-repo-1";
            var repo2 = "foo-repo-2";
            var repo3 = "foo-repo-3";
            var repos = new List<string>() { repo1, repo2, repo3 };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo1}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo2}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo3}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_With_Ssh()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateGithubScript(repos, SOURCE_ORG, TARGET_ORG, "", "", false, true);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ssh }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_GHES_Repo()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };
            var ghesApiUrl = "https://api.foo.com";
            var azureStorageConnectionString = "foo-storage-connection-string";

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateGithubScript(repos, SOURCE_ORG, TARGET_ORG, ghesApiUrl, azureStorageConnectionString, false, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Github_GHES_Repo_No_Ssl()
        {
            var repo = "foo-repo";
            var repos = new List<string>() { repo };
            var ghesApiUrl = "https://api.foo.com";
            var azureStorageConnectionString = "foo-storage-connection-string";

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateGithubScript(repos, SOURCE_ORG, TARGET_ORG, ghesApiUrl, azureStorageConnectionString, true, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --github-source-org \"{SOURCE_ORG}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{repo}\" --ghes-api-url \"{ghesApiUrl}\" --azure-storage-connection-string \"{azureStorageConnectionString}\" --no-ssl-verify }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_No_Data()
        {
            var command = new GenerateScriptCommand(null, null, null, null);
            var script = command.GenerateAdoScript(null, "foo-source", "foo-target", false);

            string.IsNullOrWhiteSpace(script).Should().BeTrue();
        }

        [Fact]
        public void Ado_Single_Repo()
        {
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";
            var repos = new Dictionary<string, IEnumerable<string>>() { { adoTeamProject, new List<string>() { repo } } };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateAdoScript(repos, SOURCE_ORG, TARGET_ORG, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo}\" }}";

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

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateAdoScript(repos, SOURCE_ORG, TARGET_ORG, false);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo1}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo1}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo2}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo2}\" }}";
            expected += Environment.NewLine;
            expected += $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo3}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo3}\" }}";

            script.Should().Be(expected);
        }

        [Fact]
        public void Ado_With_Ssh()
        {
            var adoTeamProject = "foo-team-project";
            var repo = "foo-repo";
            var repos = new Dictionary<string, IEnumerable<string>>() { { adoTeamProject, new List<string>() { repo } } };

            var command = new GenerateScriptCommand(new Mock<OctoLogger>().Object, null, null, null);
            var script = command.GenerateAdoScript(repos, SOURCE_ORG, TARGET_ORG, true);

            script = TrimNonExecutableLines(script);

            var expected = $"Exec {{ gh gei migrate-repo --ado-source-org \"{SOURCE_ORG}\" --ado-team-project \"{adoTeamProject}\" --source-repo \"{repo}\" --github-target-org \"{TARGET_ORG}\" --target-repo \"{adoTeamProject}-{repo}\" --ssh }}";

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
